<# Pester negative/positive checks for stable requirement and evidence-link enforcement. #>
Describe 'StrictDoc traceability guard' {
    BeforeEach {
    $lines=@('[DOCUMENT]','TITLE: Test requirements','')
    foreach ($number in 1..92) {
        $lines+=@('[REQUIREMENT]',('UID: SC-'+$number.ToString('000')),'STATEMENT: Test requirement.','')
    }
    $valid=$lines -join [Environment]::NewLine
    }
    It 'accepts preserved stable IDs' {
        $path=Join-Path $TestDrive 'valid.sdoc'
        Set-Content $path $valid
        & "$PSScriptRoot/Test-Requirements.ps1" -DocumentPath $path | Out-Null
    }
    It 'rejects deletion of a stable requirement' {
        $path=Join-Path $TestDrive 'missing.sdoc'
        Set-Content $path ($valid.Replace('UID: SC-001','UID: REPLACED'))
        $rejected=$false
        try { & "$PSScriptRoot/Test-Requirements.ps1" -DocumentPath $path | Out-Null } catch { $rejected=$true }
        if (-not $rejected) { throw 'Deleted UID was accepted.' }
    }
    It 'rejects duplicate IDs' {
        $path=Join-Path $TestDrive 'duplicate.sdoc'
        Set-Content $path ($valid+"`n[REQUIREMENT]`nUID: SC-001`nSTATEMENT: Duplicate.`n")
        $rejected=$false
        try { & "$PSScriptRoot/Test-Requirements.ps1" -DocumentPath $path | Out-Null } catch { $rejected=$true }
        if (-not $rejected) { throw 'Duplicate UID was accepted.' }
    }
    It 'rejects broken evidence file links' {
        $path=Join-Path $TestDrive 'broken.sdoc'
        Set-Content $path ($valid+"`nRELATIONS:`n- TYPE: File`n  VALUE: does-not-exist-evidence.txt`n")
        $rejected=$false
        try { & "$PSScriptRoot/Test-Requirements.ps1" -DocumentPath $path | Out-Null } catch { $rejected=$true }
        if (-not $rejected) { throw 'Broken file link was accepted.' }
    }
}
