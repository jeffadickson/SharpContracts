# Engineering evidence — 2026-10-24

This report records executed evidence for SC-089 and the subsequent 50-iteration SC-090 increment. Final local acceptance: 392 .NET tests and 9 Pester tests passed, zero failures/skips; five iteration compile checkpoints passed.

StrictDoc is the canonical requirement source at requirements/SharpContracts.sdoc. Existing IDs are preserved. New automation is PowerShell/.NET; Python is used only by the required StrictDoc tool and existing HTML/CLI validation workflows.

## Assignment increment (pre-iteration commit)

All nine projects compile with .NET SDK 10.0.402 targeting net8.0. Test results: 332 .NET cases passed (16 runtime, 32 solver, 284 verification), zero failures/skips. Three Pester coverage-parser checks passed. FsCheck ran 32 assignment roundtrip samples with seed 12345,67891 and 32 bounded malformed-source samples with seed 54321,98765. No randomized failure needed minimization.

Two isolated deliberate assignment mutations were killed: ignoring the assignment RHS and replacing it with zero; each produced two failing tests in an otherwise passing eight-case mutation baseline. This is a bounded two-mutant campaign, not an exhaustive Stryker score.

Coverlet 6.0.4 measured the verification-project test view: Verification 89.98% line / 75.00% branch, Solver 78.94% / 67.50%, Runtime 18.00% / 10.98%. These are separate assembly measurements from this test view, not a merged whole-solution percentage. Runtime and solver projects also produced separate reports. Native Z3, emitted replay assemblies, unloaded analyzer/CLI assemblies and PowerShell scripts are unmeasured by this collector. Coverage is not proof of correctness.

The first preliminary coverage run overlapped a rebuild and used prior binaries; it is excluded from acceptance. Final runs were serialized after the completed rebuild and stored in artifacts/coverage/assignment*. StrictDoc 0.30.4 installation/export validation remains pending and is a final gate for the subsequent increment. Requirements use the canonical .sdoc file now, with all 89 existing/new IDs preserved.

## Completed 50-iteration increment

All five compilation checkpoints (10, 20, 30, 40, 50) passed, followed by a successful final-review build. The final suite passed 392 unique .NET cases: 16 runtime, 32 solver, 344 verification. No failures/skips. The 50-case shift corpus contains 39 source-derived Z3 proofs, 9 solver counterexamples replayed against the exact compiled C# and 2 unsupported-input rejections. Additional explicit local-assignment overflow replay uses x=4294967295 and fails the expected contract P. All previous flagship, archived corpus and source-verification tests remain passing.

The shift property used FsCheck seed 20261024,1357911 for 256 inputs across 12 shift forms (3072 bit-result comparisons); fixed boundaries add 900 comparisons. Together with the assignment/fuzz properties, 320 fixed-seed property samples ran. No failing randomized input needed minimization. Solver witnesses are genuine models, not claimed minimal; structured reports, including counterexample inputs, are retained in the shift tests' TRX output.

### Measured coverage

Coverlet 6.0.4 verification-project test view: SharpContracts.Verification 90.18% line / 75.34% branch; SharpContracts.Solver 81.33% / 72.04%; SharpContracts.Runtime 18.00% / 10.98%. The new EncodeShift method measured 100% line / 100% branch coverage. The separate runtime-project view measured Runtime 70.00% / 43.40%, and Analyzers 0% / 0%. The separate solver-project view measured Solver 59.33% / 50.00%, Runtime 21.60% / 19.23%. Overlapping views are not averaged or presented as a merged solution percentage.

Nine Pester tests passed, including native CLI integration and the reviewed isolated mutation runner. Pester 3.4 measured 218/236 PowerShell commands (92.37%) in four automation files; this is command coverage, not line/branch coverage. Its missed-command report includes error paths and Unix path-comparison branches unexecuted on Windows. Pester does not provide branch coverage here. Native Z3, dynamically emitted CLR replay assemblies and the CLI entry point (executed outside the collector) are outside Coverlet instrumentation; the analyzer has no executed diagnostic tests in these measurements. Native UNKNOWN/timeout paths are not deterministically forced by this new corpus. Coverage is not a correctness proof.

### Deliberate mutations and tool checks

The final reviewed campaign used a passing 16-case baseline and killed all 6 selected compiled mutants: ignoring assignments (2 failures), replacing assigned values with zero (2), removing shift-count masking (4), applying the 32-bit mask to 64-bit shifts (2), logical shift for signed >> (2), and arithmetic shift for unsigned/>>> (4). This bounded selected-mutant result is not an exhaustive Stryker score. Baseline and each mutant have unique per-run TRX paths. Compiler/no-test runs cannot count as kills, and stale reports cannot be reused. Sources are copied to an isolated artifact directory and restored after each mutant; the checkout is never mutated by the campaign.

All 8 real CLI JSON/source-link/input/exit-code cases passed through the native PowerShell validator. HTML embedded scripts passed syntax checks. StrictDoc 0.30.4 exported the canonical requirements as HTML and JSON; the traceability guard validates 90 unique stable IDs and existing file links. StrictDoc installation was pending in the earlier pre-iteration commit and is resolved now. CI is configured to collect/upload managed coverage, validate/export StrictDoc and use the PowerShell CLI validator.

The initial implementation commit c809264c0fd9101909b9bb1e3ef2640f834e2ab7 passed GitHub CI at https://github.com/jeffadickson/SharpContracts/actions/runs/40120637145. The final increment's CI URL/conclusion is recorded in the final engineering response after publication; this document does not infer success from workflow source alone.

### Remaining scope and reproduction

SC-089 and SC-090 are verified within the supported primitive model. SC-088 remains partial: branches, early returns, CFG/SSA joins, parameter/heap mutation, custom operators, checked exceptions and method-level exit postconditions remain unsupported. The mathematical/real and BV64 prototypes are retained. Existing clean-build analyzer release-tracking and legacy nullable warnings remain; no general C# soundness, cross-project IR serialization or VeloFormal syntax migration is claimed.

Run from the checkout root: dotnet build SharpContracts.sln --configuration Release; dotnet test each test project with --collect 'XPlat Code Coverage' --settings coverage.runsettings and a separate results directory; pwsh -File tools/Get-CoverageSummary.ps1; pwsh -File tools/Test-Requirements.ps1; pwsh -File tools/Test-Cli.ps1; pwsh -File tools/Test-Mutations.ps1. Pester unit/integration tests are tools/*.Tests.ps1; the integration file performs the real mutation campaign. StrictDoc is pinned in tools/strictdoc-requirements.txt and export uses strictdoc export requirements --formats html,json --no-parallelization. PowerShell command coverage can be collected with Invoke-Pester -CodeCoverage on the four automation files.
