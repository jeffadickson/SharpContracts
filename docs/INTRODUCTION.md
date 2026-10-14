# SharpContracts — Introduction (Milestone 1)

SharpContracts is a C# Design by Contract toolkit inspired by Eiffel. Preconditions declare what callers must supply, postconditions declare what a method guarantees after a successful return, and invariants declare states that must hold at explicitly checked boundaries.

## Implemented

- Automatic interface-boundary precondition enforcement via `ContractProxy.Create<T>` and `[ContractNotNull]`/`[ContractPositive]` attributes.
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

## Automatic interface contract example

```csharp
public interface ITransferService
{
    void Send([ContractPositive("AUTO-001")] decimal amount,
              [ContractNotNull("AUTO-002")] string recipient);
}
ITransferService guarded = ContractProxy.Create<ITransferService>(new TransferService());
guarded.Send(0m, "Alice"); // Throws before invoking implementation.
```

This is a synchronous interface-boundary prototype. It does not enforce calls on the original instance, internal self-calls, arbitrary class methods, or await-time postconditions. Interface signatures must be compatible with the reflected target. General weaving is still open.

## Analyzer installation (local project)

```xml
<ProjectReference Include="../SharpContracts.Analyzers/SharpContracts.Analyzers.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

The analyzer is **not yet automatically referenced by the runtime NuGet package**; library packaging is future work.

## Known limitations

Explicit checks and automatic interface-boundary checks are implemented; general class method interception/weaving is not implemented. `Old<T>` is value capture rather than deep reference snapshots. The analyzer identifies three narrow categories only and does not establish full expression purity or contracts' satisfiability. Postcondition failure occurs after potential mutation, without rollback. The browser runs JavaScript examples and does not execute C#. No SMT-backed formal verification yet. Builds and tests should run in GitHub CI; no claim of green status until confirmed.
