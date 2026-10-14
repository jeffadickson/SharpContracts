# SharpContracts — Milestone 7

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
