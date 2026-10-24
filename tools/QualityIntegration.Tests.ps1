<#
Pester integration checks deliberately execute the real .NET CLI and isolated mutation
campaign. This file is slower than parser unit tests and retains native execution logs.
#>
Describe 'Native quality automation integration' {
    It 'validates real CLI reports, source links and replay values' {
        & "$PSScriptRoot/Test-Cli.ps1"
    }
    It 'kills the selected compiled mutants without stale result reuse' {
        & "$PSScriptRoot/Test-Mutations.ps1" -OutputDirectory artifacts/mutations-reviewed
    }
}
