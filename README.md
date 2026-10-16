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
