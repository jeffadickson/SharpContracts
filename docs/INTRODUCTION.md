# SharpContracts — Introduction (Milestone 7)

SharpContracts is a C# Design by Contract toolkit inspired by Eiffel. Preconditions declare what callers must supply, postconditions declare what a method guarantees after a successful return, and invariants declare states that must hold at explicitly checked boundaries.

## Milestone 7: branch-sensitive verification and fail-closed reporting

The verifier now explores both sides of supported `if/else` statements, maintaining independent symbolic variable assignments and branch assumptions. An `Ensure` reached on multiple paths produces a check for each path. The `AllProved` summary requires every check to be proved and no unsupported-code errors.

Unsupported statements, C# parsing errors, or a path explosion above 256 paths invalidate the entire method report; partial proof results are discarded. Regression tests cover a valid clamp, a failing else branch, malformed source, and an unsupported loop after a contract check.

This remains a **mathematical integer** proof prototype, not sound CLR `long` overflow verification. It also does not yet perform full semantic compilation, analyze arbitrary method calls, or support method return values.

## Roslyn-to-Z3 source verification — Milestone 7

The new `SharpContracts.Verification.MethodVerifier.Verify(source, methodName)` parses a C# method with Roslyn, translates a restricted set of statements to symbolic expressions, and queries the real Z3 backend for each `Contract.Ensure`.

Supported prototype: `long` parameters, initialized `long`/`var` locals, simple assignments, integer literals, addition, subtraction, Boolean comparisons, and positional `Contract.Require`/`Contract.Ensure` calls. Results include proof status, source line, contract ID, and raw counterexample model.

Unsupported branches, loops, arbitrary calls, async methods, reference objects, and exceptions fail closed. **Crucial limitation:** integer expressions currently use unbounded SMT mathematical integers rather than 64-bit overflow semantics. Results do not establish correctness of all actual CLR executions. Compilation, symbol resolution, and checked/unchecked arithmetic modeling remain future work.

```csharp
var report = MethodVerifier.Verify(source, "Transfer");
foreach (var check in report.Checks)
    Console.WriteLine($"{check.Line}: {check.ContractId}: {check.Status}");
```

## Actual solver integration (Milestone 4)

`SharpContracts.Solver` now depends on `Microsoft.Z3` 4.12.2. The `Z3Verifier.Verify(assumptions, claim)` API asserts all assumptions plus the **negation** of the desired claim. Z3 returns:

- `Proved` if the counterexample formula is UNSAT.
- `Counterexample` and a model if SAT.
- `Unknown` if the solver cannot decide, including timeout.

Example:

```csharp
var n = Expr.Param("n", SymbolicType.Integer);
var result = Z3Verifier.Verify(
    new[] { Expr.Gt(n, Expr.Int(5)) },
    Expr.Gt(n, Expr.Int(0)));
Console.WriteLine(result.Status); // Proved (assuming solver completes)
```

A sample transfer conservation property also uses the solver over **unbounded mathematical integers**, not .NET overflow-sensitive integers. Decimal maps to mathematical Real, without .NET rounding/overflow. This is a proof of the provided formula **under the encoded assumptions**, not of arbitrary C# method bodies. There is not yet automatic VC extraction from C# code. The browser demo remains a teaching approximation and does not execute the solver; the actual Z3 integration lives in the .NET project and CI tests. Build and test execution have not been verified here.

## Implemented\n\n- Symbolic expression tree: typed parameters, constants, Boolean connectives, arithmetic and comparisons.\n- Runtime evaluation from a supplied name/value environment and SMT-LIB assertion generation, and separate actual Z3 solver verification.

- Typed `ContractDefinition<T>`, `ContractSet<T>` and `ContractBoundary.Invoke` for reusable runtime requirements and guarantees.
- Automatic synchronous return-value checking through `[ContractNonNegativeResult]` on an interface method, using the proxy.
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

## Symbolic contracts

```csharp
using SharpContracts.Symbolic;
var rule = Expr.Gt(Expr.Param("amount", SymbolicType.Decimal), Expr.Dec(0m));
var environment = new Dictionary<string,object> { ["amount"] = 25m };
bool accepted = ContractExprEvaluator.Check(rule, environment);
string solverInput = SmtLibPrinter.Print(rule);
```

The expression is declarative, unlike a `Func<T,bool>` delegate, and can be inspected without executing arbitrary user code. `SmtLibPrinter` generates a satisfiability query for the predicate; it **does not prove** the predicate holds for every possible input. Proof obligations would need negated counterexample queries with assumptions and well-specified program semantics. Decimal values are exported as SMT Real, which does not automatically preserve .NET decimal overflow and rounding behavior.

## Reusable typed pre/postconditions

```csharp
var pre = new ContractSet<int>()
    .Add(new("REQ-POS", ContractKind.Precondition, "must be positive", n => n > 0));
var post = new ContractSet<(int Input, int Result)>()
    .Add(new("ENS-SQUARE", ContractKind.Postcondition, "must square", x => x.Result == x.Input * x.Input));
int answer = ContractBoundary.Invoke(3, n => n*n, pre, post);
```

These are typed executable predicates, **not yet** a solver-friendly symbolic IR. An arbitrary delegate cannot be safely translated to an SMT expression.

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

The proxy additionally checks a supported `[ContractNonNegativeResult]` annotation after a synchronous interface call returns normally. This is a synchronous interface-boundary prototype. It does not enforce calls on the original instance, internal self-calls, arbitrary class methods, or await-time postconditions. Interface signatures must be compatible with the reflected target. General weaving is still open.

## Analyzer installation (local project)

```xml
<ProjectReference Include="../SharpContracts.Analyzers/SharpContracts.Analyzers.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

The analyzer is **not yet automatically referenced by the runtime NuGet package**; library packaging is future work.

## Known limitations

Explicit checks and automatic interface-boundary checks are implemented; general class method interception/weaving is not implemented. `Old<T>` is value capture rather than deep reference snapshots. The analyzer identifies three narrow categories only and does not establish full expression purity or contracts' satisfiability. Postcondition failure occurs after potential mutation, without rollback. The browser runs JavaScript examples and does not execute C#. No SMT-backed formal verification yet. Builds and tests should run in GitHub CI; no claim of green status until confirmed.
