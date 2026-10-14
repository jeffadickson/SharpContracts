# SharpContracts — Introduction (Milestone 1)

SharpContracts is a C# Design by Contract toolkit inspired by Eiffel. Preconditions declare what callers must supply, postconditions declare what a method guarantees after a successful return, and invariants declare states that must hold at explicitly checked boundaries.

## Implemented

- Runtime `Contract.Require`, `Contract.Ensure`, `Contract.Invariant` with stable identifiers and typed violation exceptions.
- Experimental Roslyn analyzer with three diagnostics: **SC1001** blank constant contract ID; **SC1002** compile-time false condition; **SC1003** syntactic assignment or increment/decrement inside the condition.
- xUnit runtime tests and GitHub Actions build/test workflow.
- Standalone browser JavaScript contract simulator and analyzer rule explorer in `index.html`.

## Example

```csharp
Contract.Require(amount > 0m, "PRE-004", "Positive transfer required");
decimal before = Contract.Old(from.Balance);
from.Debit(amount);
Contract.Ensure(from.Balance == before - amount, "POST-001", "Correct debit");
```

## Analyzer installation (local project)

```xml
<ProjectReference Include="../SharpContracts.Analyzers/SharpContracts.Analyzers.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

The analyzer is **not yet automatically referenced by the runtime NuGet package**; library packaging is future work.

## Known limitations

Checks are explicit; general method interception/weaving is not implemented. `Old<T>` is value capture rather than deep reference snapshots. The analyzer identifies three narrow categories only and does not establish full expression purity or contracts' satisfiability. Postcondition failure occurs after potential mutation, without rollback. The browser runs JavaScript examples and does not execute C#. No SMT-backed formal verification yet. Builds and tests should run in GitHub CI; no claim of green status until confirmed.
