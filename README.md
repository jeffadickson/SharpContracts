## 100-case CLR + source-derived Z3 acceptance gate

The archived 100-case regression corpus is now in `SharpContracts.Verification.Tests/Corpus100Tests.cs` with its original JSON data preserved as a gzip/base64 fixture. The test project also compiles the three annotated C# showcase methods and checks their actual runtime contract failures. CI explicitly runs the corpus and the source-derived Z3 proof tests for midpoint, payment splitting, and buffer admission.

These are **committed acceptance tests, not observed passing results**. A green CI Gate run is still required before calling the milestone verified. [Actions](https://github.com/jeffadickson/SharpContracts/actions).

# SharpContracts — Inline C# Contracts

**New primary example:** [annotated binary-search midpoint source](examples/BinarySearchMidpoint.cs) verified without handwritten SMT expressions.

```sh
dotnet run --project SharpContracts.Cli -- --inline examples/BinarySearchMidpoint.cs Correct
dotnet run --project SharpContracts.Cli -- --inline --json examples/BinarySearchMidpoint.cs Buggy
```

The runtime library now provides `[Verify]` and `Contract.Assert` alongside `Require` and `Ensure`. The experimental inline Roslyn-to-Z3 backend handles the documented UInt32 subset. Its tests read the same annotated source file. **CI compilation and solver execution remain unconfirmed.**

Read the [authoring and verification design](docs/INLINE_CONTRACT_DESIGN.md) and [interactive tutorial](tutorial/index.html).

## Featured example: binary-search midpoint overflow

[Interactive walkthrough](tutorial/index.html#midpoint-lesson) · [C# sample](examples/BinarySearchMidpoint.cs) · [Z3 proof regression](SharpContracts.Solver.Tests/MidpointProofTests.cs)

The expression `unchecked((low + high) / 2u)` can overflow before division. For `low=3,000,000,000` and `high=4,000,000,000` it incorrectly returns `1,352,516,352`. The corrected `low + (high-low)/2u` returns `3,500,000,000`. Z3 checks whether the midpoint is inside the range for **all** valid UInt32 pairs. These tests are committed but have not been observed passing in CI.

## CI Gate status debugging

`CI Gate` now prints each of its six dependencies by name and writes a per-job table into the GitHub Actions job summary. For actual results, open [Actions](https://github.com/jeffadickson/SharpContracts/actions) → latest CI run → **CI Gate**. An empty legacy commit-status API response is not evidence of no GitHub Actions runs; Actions check runs are a separate API. This change is committed but has not yet been observed executing.


## Unary negation increment

The Roslyn-bound verifier now lowers unchecked unary minus to Z3 bit-vector negation and rejects checked unary negation until exceptional behavior is modeled. Regression tests cover counterexamples and unsupported syntax. Build/CI results are not confirmed.

# CI aggregate gate

The `CI Gate` job now runs after all six workflow jobs and fails if any compilation, test, CLI, solver or tutorial job failed, was skipped or was cancelled. It is a single status-check target for future branch protection. GitHub Actions run status remains unverified.

# Verification soundness fix

Compiler-bound verification now rejects dynamic contract message expressions and blank IDs, preventing unmodeled calls in contract arguments from being silently treated as pure. Tests are committed but not executed locally; passing CI remains unconfirmed.

# CI stabilization — complete build diagnostics

The `compile-all` job now attempts **all nine projects**, even if an earlier build fails. It captures `build.log` and `summary.log`, uploads both as the `sharpcontracts-build-log` artifact, and fails the workflow if any project fails. This fixes early termination of diagnostic collection; passing CI has **not** been confirmed.

# CI compilation gate

A dedicated `compile-all` GitHub Actions job now builds all nine production/test `.csproj` projects on .NET 8 and uploads `sharpcontracts-build-log` (14 days) even if compilation fails. This exposes compile errors before solver-runtime investigation. Successful CI execution is still unverified.

# SharpContracts — CI diagnostics increment

GitHub Actions now writes `.trx` test reports for the runtime, Z3 solver, and source verifier jobs and uploads them as artifacts even when tests fail. Open [Actions](https://github.com/jeffadickson/SharpContracts/actions), select a CI run, and download `sharpcontracts-runtime-test-results`, `sharpcontracts-solver-test-results`, or `sharpcontracts-verification-test-results`. This does **not** establish a passing build; these artifacts are intended to expose the exact failing tests.

# SharpContracts — Milestone 18: CI report validation

The CI CLI smoke job now runs `scripts/check_cli_report.py`, which invokes the compiled .NET verification CLI and validates JSON schema version, outcome, nonempty checks, and exit codes for both a proof and an overflow counterexample. **Workflow execution is not yet confirmed**, so CI is not claimed green.

# SharpContracts — Milestone 17

ProofReport adds versioned JSON results and a richer CLI exit-code contract: 0 proved, 1 counterexample, 2 unsupported/no contracts, 3 unknown, 4 inconsistent assumptions. Use `dotnet run --project SharpContracts.Cli -- --json examples/VerifiedByteAddition.cs Check`. This remains unverified by a successful .NET build.

# SharpContracts — Milestone 16

**Proof-soundness improvement:** the compiler-bound verifier checks that preconditions are jointly satisfiable before accepting an implication proof. Contradictory assumptions now fail rather than yield a misleading vacuous proof. Regression tests cover contradictory and feasible preconditions. Test execution remains unverified.

# SharpContracts — Milestone 15

**Milestone 14:** Fixed UINT64 literal lowering above long.MaxValue and added boundary proof/counterexample tests. Build and CI remain unverified.

# SharpContracts — Milestone 13

A research-stage Design by Contract and SMT verification framework for C#.

## Milestone 15 — verification guardrails

The compiler-bound solver now has a five-second per-query timeout. Numeric conversion and comparison lowering rejects unsupported numeric representations instead of assuming built-in integer bit-vector behavior. Negative regression tests cover decimal and custom overloaded operators. These are source changes; .NET compilation and CI remain unverified.

## Try the actual verification CLI

```sh
dotnet run --project SharpContracts.Cli -- examples/VerifiedByteAddition.cs Check
dotnet run --project SharpContracts.Cli -- examples/OverflowCounterexample.cs Check
```

The first sample is expected to be provable and return exit code 0. The second intentionally violates its postcondition at signed 32-bit overflow and should return exit code 1. Unsupported expressions, compiler errors, or no checked contracts return code 2. These outcomes are **expected**, not confirmed: the .NET build and CI runs have not been verified.

## Learn

[Interactive five-chapter tutorial](tutorial/index.html) · [Main demo](index.html) · [Technical introduction](docs/INTRODUCTION.md) · [Requirements](docs/REQUIREMENTS.md) · [Actions](https://github.com/jeffadickson/SharpContracts/actions)

## Verified subset and limitations

`BoundNumericVerifier` uses Roslyn semantic operations and Z3 bit-vectors for a restricted parameter-only method subset. User-defined operators, checked arithmetic, local state, arbitrary control flow and many language features are not supported by this entry point. A mathematical-int model, BV64 prototype, and other experimental components also exist. Treat proofs as statements about the supported model only.

## C# versus Z3 differential checks

`ClrDifferentialTests` compares prover outcomes to compiled C# at `int.MaxValue`, `ulong.MaxValue`, and all 65,536 combinations of two `byte` inputs. Checked overflow must be rejected by the current verifier and throw in CLR execution. This is a regression suite, not a formal validation of all language semantics. GitHub CI execution has not yet been confirmed.

## Build

```sh
dotnet test SharpContracts.Tests/SharpContracts.Tests.csproj
dotnet test SharpContracts.Solver.Tests/SharpContracts.Solver.Tests.csproj
dotnet test SharpContracts.Verification.Tests/SharpContracts.Verification.Tests.csproj
dotnet build SharpContracts.Cli/SharpContracts.Cli.csproj
```

CI is defined in `.github/workflows/ci.yml` for pushes, pull requests and manual dispatch. Check Actions for actual results; merely having a workflow file does not establish a green build.

---

## Historical milestone details

# SharpContracts — Milestone 11

## Start here: interactive tutorial

Open **[From C# Contracts to Z3 Proofs](tutorial/index.html)** for a five-chapter, browser-runnable introduction to contracts, counterexamples, overflow, C# numeric promotion, and running the .NET project. The tutorial uses JavaScript teaching demonstrations rather than executing Z3. 

[Main interactive demo](index.html) · [Technical introduction](docs/INTRODUCTION.md) · [CI workflow](.github/workflows/ci.yml)


A Design by Contract framework for C#.

## Milestone 1\n\nAdded Roslyn-to-Z3 verification for a restricted C# method subset. Previously added an actual Z3 verifier (SharpContracts.Solver) and dedicated solver tests. Previously added symbolic contract expressions, executable evaluator, and SMT-LIB export (with actual solver invocation via SharpContracts.Solver). Previously added reusable typed contract definitions and boundary wrappers plus synchronous return-value guarantees. Previously added interface-boundary automatic precondition enforcement (via `ContractProxy`), a Roslyn analyzer (SC1001–SC1003), xUnit tests, GitHub Actions CI and an interactive analyzer teaching exercise. See [introduction](docs/INTRODUCTION.md).\n\n## Interactive prototype
Open `index.html` from the downloadable Milestone 0 package in the ChatGPT project conversation. Browser prototype runs a JavaScript semantic model, not compiled C#.

## .NET example
Requires .NET 8 SDK or newer:

```sh
dotnet run --project SharpContracts.Demo
```

The runtime currently requires explicit calls to `Contract.Require`, `Contract.Ensure`, and `Contract.Invariant`. General method weaving, complete Roslyn coverage, solver integration and transactional rollback are planned, not implemented. CI workflow is committed; check Actions for build results.

See `docs/REQUIREMENTS.md` for requirements and limitations.\n\nTo reference the experimental analyzer, add it as an Analyzer project reference (instructions in `docs/INTRODUCTION.md`).


## Custom numeric types

[Design and current support](docs/CUSTOM_TYPES.md). User-defined operator semantics are not yet verified; they must not be assumed to be built-in arithmetic.
