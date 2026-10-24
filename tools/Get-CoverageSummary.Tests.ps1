<# Pester checks that coverage automation does not fabricate or double-count measurements. #>
Describe 'Measured coverage summary' {
    It 'reports measured rates and deduplicates identical attachments' {
        $inputs=Join-Path $TestDrive 'measurements'
        New-Item -ItemType Directory -Force $inputs,(Join-Path $inputs 'attachment') | Out-Null
        $xml='<coverage><packages><package name="Example" line-rate="0.5" branch-rate="0.25" /></packages></coverage>'
        Set-Content (Join-Path $inputs 'coverage.cobertura.xml') $xml
        Set-Content (Join-Path $inputs 'attachment/coverage.cobertura.xml') $xml
        $output=Join-Path $TestDrive 'summary.json'
        & "$PSScriptRoot/Get-CoverageSummary.ps1" -ResultsDirectory $inputs -OutputPath $output | Out-Null
        $rows=@(Get-Content $output -Raw | ConvertFrom-Json)
        if ($rows.Count -ne 1 -or $rows[0].LinePercent -ne 50 -or $rows[0].BranchPercent -ne 25) {
            throw 'Measured rates were changed or attachment copies were double-counted.'
        }
    }
    It 'rejects a directory without reports' {
        $empty=Join-Path $TestDrive 'empty'
        New-Item -ItemType Directory -Force $empty | Out-Null
        $rejected=$false
        try { & "$PSScriptRoot/Get-CoverageSummary.ps1" -ResultsDirectory $empty | Out-Null }
        catch { $rejected=$true }
        if (-not $rejected) { throw 'Missing measurements were accepted.' }
    }
    It 'rejects malformed coverage input' {
        $broken=Join-Path $TestDrive 'broken'
        New-Item -ItemType Directory -Force $broken | Out-Null
        Set-Content (Join-Path $broken 'coverage.cobertura.xml') '<not-xml'
        $rejected=$false
        try { & "$PSScriptRoot/Get-CoverageSummary.ps1" -ResultsDirectory $broken | Out-Null }
        catch { $rejected=$true }
        if (-not $rejected) { throw 'Malformed measurements were accepted.' }
    }
}
