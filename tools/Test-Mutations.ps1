<#
.SYNOPSIS
Runs a bounded, reviewable mutation campaign in an isolated copy of this checkout.
.DESCRIPTION
Each mutant must compile and produce a failing xUnit TRX result to count as killed.
Build failures and missing reports are errors, never successful mutation detection.
#>
[CmdletBinding()]
param([string]$OutputDirectory='artifacts/mutations')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$output=[IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
if (-not $output.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Mutation outputs must stay inside the checkout.'
}
$work=Join-Path $output ('work-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work -Force | Out-Null
# Copy source inputs, never the Git database, active build outputs or another mutation run.
$files=@(Get-ChildItem -LiteralPath $root -File)
foreach ($directory in (Get-ChildItem -LiteralPath $root -Directory |
    Where-Object { $_.Name -like 'SharpContracts.*' -or $_.Name -eq 'examples' })) {
    $files+=Get-ChildItem -LiteralPath $directory.FullName -File -Recurse |
        Where-Object FullName -NotMatch '[\\/](bin|obj)[\\/]'
}
foreach ($file in $files) {
    $relative=[IO.Path]::GetRelativePath($root,$file.FullName)
    $destination=Join-Path $work $relative
    New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
}
$target=Join-Path $work 'SharpContracts.Verification/InlineContractVerifier.cs'
$original=Get-Content -LiteralPath $target -Raw
$mutants=@(
    @{ Name='ignore-local-update'; From='values[target.Local]=value;'; To='values[target.Local]=values[target.Local];' },
    @{ Name='erase-assignment-value'; From='values[target.Local]=value;'; To='values[target.Local]=new ClrConstant(0,ClrScalarType.Integral(32,false));' }
)
$results=[System.Collections.Generic.List[object]]::new()
Push-Location $work
try {
    & dotnet test SharpContracts.Verification.Tests --configuration Release --filter 'FullyQualifiedName~LocalAssignmentTests' *> (Join-Path $output 'baseline.log')
    if ($LASTEXITCODE -ne 0) { throw 'Mutation baseline failed; no mutation score is valid.' }
    foreach ($mutant in $mutants) {
        if ([regex]::Matches($original,[regex]::Escape($mutant.From)).Count -ne 1) { throw 'Mutation site is not unique.' }
        Set-Content -LiteralPath $target -Value $original.Replace($mutant.From,$mutant.To) -Encoding utf8
        $resultPath=Join-Path $output $mutant.Name
        & dotnet test SharpContracts.Verification.Tests --configuration Release --no-restore --filter 'FullyQualifiedName~LocalAssignmentTests' --logger 'trx;LogFileName=mutation.trx' --results-directory $resultPath *> (Join-Path $output ($mutant.Name+'.log'))
        $exitCode=$LASTEXITCODE
        $trxPath=Join-Path $resultPath 'mutation.trx'
        if (-not (Test-Path -LiteralPath $trxPath)) { throw ('Mutant did not execute tests: '+$mutant.Name) }
        [xml]$trx=Get-Content -LiteralPath $trxPath -Raw
        $failed=[int]$trx.TestRun.ResultSummary.Counters.failed
        $status=if ($exitCode -ne 0 -and $failed -gt 0) {'Killed'} elseif ($exitCode -eq 0) {'Survived'} else {'Error'}
        $results.Add([pscustomobject]@{ Mutant=$mutant.Name; Status=$status; FailedTests=$failed })
        Set-Content -LiteralPath $target -Value $original -Encoding utf8
    }
}
finally { Set-Content -LiteralPath $target -Value $original -Encoding utf8; Pop-Location }
$results | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'results.json') -Encoding utf8
$results | Format-Table
if (@($results | Where-Object Status -NE 'Killed').Count -gt 0) { throw 'Mutation campaign has surviving or errored mutants.' }
