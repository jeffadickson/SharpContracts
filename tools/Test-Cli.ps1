<# .SYNOPSIS Executes native .NET/Z3 CLI cases and checks JSON outcomes and exit codes. #>
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$cases=@(
    @{File='VerifiedByteAddition'; Method='Check'; Inline=$false; Code=0; Outcome='Proved'},
    @{File='OverflowCounterexample'; Method='Check'; Inline=$false; Code=1; Outcome='Counterexample'}
)
foreach ($file in @('BinarySearchMidpoint','PaymentSplit','BufferAdmission')) {
    $cases+=@{File=$file; Method='Correct'; Inline=$true; Code=0; Outcome='Proved'}
    $cases+=@{File=$file; Method='Buggy'; Inline=$true; Code=1; Outcome='Counterexample'}
}
foreach ($case in $cases) {
    $arguments=@('SharpContracts.Cli/bin/Release/net8.0/SharpContracts.Cli.dll','--json')
    if ($case.Inline) { $arguments+='--inline' }
    $arguments+=@(('examples/'+$case.File+'.cs'),$case.Method)
    $json=& dotnet @arguments
    $code=$LASTEXITCODE
    $report=$json | ConvertFrom-Json
    if ($code -ne $case.Code -or $report.exitCode -ne $code -or $report.outcome -ne $case.Outcome -or
        $report.schemaVersion -ne 1 -or @($report.checks).Count -eq 0 -or @($report.diagnostics).Count -ne 0) {
        throw ('CLI contract mismatch: '+$case.File+' '+$case.Method)
    }
    Write-Output ('PASS '+$case.File+' '+$case.Method+': '+$report.outcome+' exit='+$code)
}
