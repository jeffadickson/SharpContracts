<#
.SYNOPSIS
Reports measured managed line/branch coverage separately for each test run and assembly.
.DESCRIPTION
Deduplicates identical VSTest attachment copies; does not average overlapping reports.
Native Z3, dynamically emitted replay assemblies and
unloaded production assemblies are outside these collector measurements.
#>
[CmdletBinding()]
param([string]$ResultsDirectory='artifacts/coverage', [string]$OutputPath='artifacts/coverage-summary.json')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$reports=@(Get-ChildItem -LiteralPath $ResultsDirectory -Filter coverage.cobertura.xml -File -Recurse |
    Group-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } |
    ForEach-Object { $_.Group[0] })
if ($reports.Count -eq 0) { throw 'No measured Cobertura reports found.' }
$rows=foreach ($report in $reports) {
    [xml]$coverage=Get-Content -LiteralPath $report.FullName -Raw
    foreach ($package in $coverage.coverage.packages.package) {
        [pscustomobject]@{
            Report=$report.FullName
            Assembly=$package.name
            LinePercent=[math]::Round(100*[double]::Parse($package.'line-rate',[cultureinfo]::InvariantCulture),2)
            BranchPercent=[math]::Round(100*[double]::Parse($package.'branch-rate',[cultureinfo]::InvariantCulture),2)
        }
    }
}
if (@($rows).Count -eq 0) { throw 'No measured managed assembly packages found.' }
$rows | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding utf8
$rows | Format-Table Assembly,LinePercent,BranchPercent,Report -AutoSize
