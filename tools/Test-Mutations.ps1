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
$comparison=if ([IO.Path]::DirectorySeparatorChar -eq '\') { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
if (-not $output.StartsWith($root+[IO.Path]::DirectorySeparatorChar,$comparison)) {
    throw 'Mutation outputs must stay inside the checkout.'
}
$runId=[guid]::NewGuid().ToString('N')
$work=Join-Path $output ('work-'+$runId)
$runResults=Join-Path $output ('results-'+$runId)
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
$assignmentFile='SharpContracts.Verification/InlineControlFlow.cs'
$shiftFile='SharpContracts.Solver/ClrExpressionEncoder.cs'
$originals=@{}
foreach ($file in @($assignmentFile,$shiftFile)) { $originals[$file]=Get-Content -LiteralPath (Join-Path $work $file) -Raw }
$mutants=@(
    @{ Name='ignore-local-update'; File=$assignmentFile; From='path.Values[store.Name]=value;'; To='path.Values.TryAdd(store.Name,value);' },
    @{ Name='erase-assignment-value'; File=$assignmentFile; From='path.Values[store.Name]=value;'; To='path.Values[store.Name]=new Value(new ClrConstant(0,UInt32));' },
    @{ Name='shared-branch-values'; File=$assignmentFile; From='new(Values,StringComparer.Ordinal)'; To='Values' },
    @{ Name='wrong-false-guard'; File=$assignmentFile; From='path.Fork(context.MkNot(condition))'; To='path.Fork(condition)' },
    @{ Name='ignore-return-termination'; File=$assignmentFile; From='break; // A returned path never reaches a later source contract.'; To='next.Add(path); break; // Deliberate mutation.' },
    @{ Name='omit-false-arm-validation'; File=$assignmentFile; From='branch.WhenFalse is null ? null : Compile(branch.WhenFalse,depth+1)'; To='null' },
    @{ Name='unmasked-shift-count'; File=$shiftFile; From='uint countMask=type.Width==64 ? 63u : 31u;'; To='uint countMask=uint.MaxValue;' },
    @{ Name='wrong-64-bit-mask'; File=$shiftFile; From='uint countMask=type.Width==64 ? 63u : 31u;'; To='uint countMask=31u;' },
    @{ Name='logical-for-signed-right'; File=$shiftFile; From='context.MkBVASHR(operand,distance)'; To='context.MkBVLSHR(operand,distance)' },
    @{ Name='arithmetic-for-unsigned-right'; File=$shiftFile; From='_ => context.MkBVLSHR(operand,distance)'; To='_ => context.MkBVASHR(operand,distance)' }
)
$results=[System.Collections.Generic.List[object]]::new()
Push-Location $work
try {
    $filter='FullyQualifiedName~LocalAssignmentTests|FullyQualifiedName~ShiftBoundaryTests|FullyQualifiedName~ControlFlowSanityTests'
    $baselinePath=Join-Path $runResults 'baseline'
    & dotnet test SharpContracts.Verification.Tests --configuration Release --filter $filter --logger 'trx;LogFileName=baseline.trx' --results-directory $baselinePath *> (Join-Path $output 'baseline.log')
    if ($LASTEXITCODE -ne 0) { throw 'Mutation baseline failed; no mutation score is valid.' }
    [xml]$baseline=Get-Content -LiteralPath (Join-Path $baselinePath 'baseline.trx') -Raw
    if ([int]$baseline.TestRun.ResultSummary.Counters.total -le 0) { throw 'Mutation baseline executed no tests.' }
    foreach ($mutant in $mutants) {
        $target=Join-Path $work $mutant.File
        $original=$originals[$mutant.File]
        if ([regex]::Matches($original,[regex]::Escape($mutant.From)).Count -ne 1) { throw 'Mutation site is not unique.' }
        Set-Content -LiteralPath $target -Value $original.Replace($mutant.From,$mutant.To) -Encoding utf8
        # A new result path per run prevents a stale TRX from disguising a compiler failure.
        $resultPath=Join-Path $runResults $mutant.Name
        & dotnet test SharpContracts.Verification.Tests --configuration Release --no-restore --filter $filter --logger 'trx;LogFileName=mutation.trx' --results-directory $resultPath *> (Join-Path $output ($mutant.Name+'.log'))
        $exitCode=$LASTEXITCODE
        $trxPath=Join-Path $resultPath 'mutation.trx'
        if (-not (Test-Path -LiteralPath $trxPath)) { throw ('Mutant did not execute tests: '+$mutant.Name) }
        [xml]$trx=Get-Content -LiteralPath $trxPath -Raw
        $failed=[int]$trx.TestRun.ResultSummary.Counters.failed
        $status=if ($exitCode -ne 0 -and $failed -gt 0) {'Killed'} elseif ($exitCode -eq 0) {'Survived'} else {'Error'}
        $results.Add([pscustomobject]@{ RunId=$runId; Mutant=$mutant.Name; Status=$status; FailedTests=$failed; Report=$trxPath })
        Set-Content -LiteralPath $target -Value $original -Encoding utf8
    }
}
finally {
    foreach ($file in $originals.Keys) { Set-Content -LiteralPath (Join-Path $work $file) -Value $originals[$file] -Encoding utf8 }
    Pop-Location
}
$results | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'results.json') -Encoding utf8
$results | Format-Table
if (@($results | Where-Object Status -NE 'Killed').Count -gt 0) { throw 'Mutation campaign has surviving or errored mutants.' }
