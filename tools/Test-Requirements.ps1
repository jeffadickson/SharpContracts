<#
.SYNOPSIS
Checks stable requirement IDs and tracked implementation/evidence links before StrictDoc export.
.DESCRIPTION
StrictDoc validates its own grammar. This additional gate rejects deleted/duplicate stable IDs
and broken repository file links rather than silently exporting incomplete traceability.
#>
[CmdletBinding()]
param([string]$DocumentPath='requirements/SharpContracts.sdoc')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$comparison=if ([IO.Path]::DirectorySeparatorChar -eq '\') { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
$text=Get-Content -LiteralPath $DocumentPath -Raw
$ids=@([regex]::Matches($text,'(?m)^UID: (SC-\d{3})\r?$') | ForEach-Object { $_.Groups[1].Value })
if ($ids.Count -ne @($ids | Sort-Object -Unique).Count) { throw 'Duplicate requirement UID.' }
foreach ($number in 1..90) {
    $expected='SC-'+$number.ToString('000',[cultureinfo]::InvariantCulture)
    if ($ids -notcontains $expected) { throw ('Missing stable requirement '+$expected) }
}
foreach ($match in [regex]::Matches($text,'(?m)^- TYPE: File\r?\n  VALUE: ([^\r\n]+)')) {
    $relative=$match.Groups[1].Value
    $path=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if (-not $path.StartsWith($root+[IO.Path]::DirectorySeparatorChar,$comparison) -or
        -not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw ('Invalid requirement file relation: '+$relative)
    }
}
Write-Output ('Validated '+$ids.Count+' unique stable requirements and their file links.')
