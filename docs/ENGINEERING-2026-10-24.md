# Engineering evidence — 2026-10-24

This report records executed evidence for SC-089 and subsequent 50-iteration work. Results will be filled after each completed run; no pending measurement is a passing result.

StrictDoc is the canonical requirement source at requirements/SharpContracts.sdoc. Existing IDs are preserved. New automation is PowerShell/.NET; Python is used only by the required StrictDoc tool and existing HTML/CLI validation workflows.

## Assignment increment (pre-iteration commit)

All nine projects compile with .NET SDK 10.0.402 targeting net8.0. Test results: 332 .NET cases passed (16 runtime, 32 solver, 284 verification), zero failures/skips. Three Pester coverage-parser checks passed. FsCheck ran 32 assignment roundtrip samples with seed 12345,67891 and 32 bounded malformed-source samples with seed 54321,98765. No randomized failure needed minimization.

Two isolated deliberate assignment mutations were killed: ignoring the assignment RHS and replacing it with zero; each produced two failing tests in an otherwise passing eight-case mutation baseline. This is a bounded two-mutant campaign, not an exhaustive Stryker score.

Coverlet 6.0.4 measured the verification-project test view: Verification 89.98% line / 75.00% branch, Solver 78.94% / 67.50%, Runtime 18.00% / 10.98%. These are separate assembly measurements from this test view, not a merged whole-solution percentage. Runtime and solver projects also produced separate reports. Native Z3, emitted replay assemblies, unloaded analyzer/CLI assemblies and PowerShell scripts are unmeasured by this collector. Coverage is not proof of correctness.

The first preliminary coverage run overlapped a rebuild and used prior binaries; it is excluded from acceptance. Final runs were serialized after the completed rebuild and stored in artifacts/coverage/assignment*. StrictDoc 0.30.4 installation/export validation remains pending and is a final gate for the subsequent increment. Requirements use the canonical .sdoc file now, with all 89 existing/new IDs preserved.
