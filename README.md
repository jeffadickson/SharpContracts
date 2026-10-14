# SharpContracts — Milestone 0

A Design by Contract framework for C#.

## Interactive prototype
Open `index.html` from the downloadable Milestone 0 package in the ChatGPT project conversation. Browser prototype runs a JavaScript semantic model, not compiled C#.

## .NET example
Requires .NET 8 SDK or newer:

```sh
dotnet run --project SharpContracts.Demo
```

The runtime currently requires explicit calls to `Contract.Require`, `Contract.Ensure`, and `Contract.Invariant`. Automatic weaving, Roslyn diagnostics, solver integration and transactional rollback are planned, not implemented. Build remains unverified in the creation environment.

See `docs/REQUIREMENTS.md` for requirements and limitations.
