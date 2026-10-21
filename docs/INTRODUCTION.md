## Language specification

The canonical [SharpContracts language specification](LANGUAGE_SPEC.md) defines inline C# contract syntax, current semantics and proposed extensions. The separate [VeloFormal v0.2 language specification](https://github.com/jeffadickson/VeloFormal/blob/main/docs/contract-language-v0.2.md) remains authoritative for temporal RTL contracts.

## Verified-core acceptance test integration

Three separate evidence layers now exist in the xUnit verification project:

1. `Corpus100Tests` runs 100 archived concrete UInt32 vectors against CLR arithmetic.
2. `ShowcaseClrExecutionTests` executes the actual annotated midpoint/payment/buffer methods and checks runtime contract violations.
3. `InlineMidpointTests` and `SalesShowcaseTests` invoke the source-derived Roslyn-to-Z3 verifier, expecting corrected methods to prove and defective methods to produce counterexamples.

The corpus is stored losslessly as `fixtures/corpus100.json.gz.b64`; the test decompresses it at runtime. CI executes the suites explicitly and uploads TRX files. **No successful CI execution has been observed through the available GitHub connector.** The legacy status endpoint and PR-only run lookup cannot establish the status of push-triggered Actions jobs.

# Inline contracts — source-derived proofs

The midpoint example now demonstrates the desired C# authoring model:

```csharp
[Verify]
public static uint Correct(uint low, uint high)
{
    Contract.Require(low <= high, "MID-PRE", "Valid search interval");
    uint midpoint = unchecked(low + (high - low) / 2u);
    Contract.Assert(midpoint >= low, "MID-LOW", "Not below low");
    Contract.Ensure(midpoint <= high, "MID-HIGH", "Not above high");
    return midpoint;
}
```

A corresponding `Buggy` method uses `unchecked((low + high) / 2u)`. **Both proofs are generated from the actual annotated C# source file**, using `InlineContractVerifier` (Roslyn operations to Z3 BV32), not handwritten solver expressions.

Try:

```sh
dotnet run --project SharpContracts.Cli -- --inline examples/BinarySearchMidpoint.cs Correct
dotnet run --project SharpContracts.Cli -- --inline --json examples/BinarySearchMidpoint.cs Buggy
dotnet test SharpContracts.Verification.Tests/SharpContracts.Verification.Tests.csproj
```

Expected correct method: Proved (exit 0). Expected buggy method: Counterexample (exit 1). No successful .NET build or CI run has been independently verified.

The current `Ensure` means an inline assertion **at its source location**, not a postcondition automatically enforced after arbitrary returns. The verifier currently supports a deliberately limited UInt32 method subset; see [full design and limitations](INLINE_CONTRACT_DESIGN.md).

## Case study: a real arithmetic bug hidden by ordinary tests

Binary-search midpoint computation can overflow with unsigned integer indexes. An incorrect midpoint is `unchecked((low+high)/2u)`; a corrected midpoint for `low<=high` is `unchecked(low+(high-low)/2u)`. A dedicated Z3 test asserts `low<=high` and negates the midpoint-range guarantee. It should find SAT for the buggy expression and UNSAT for the corrected expression. The tests also check ordinary and boundary CLR examples. This is a direct, self-contained Z3 bit-vector proof test, **not** verification automatically extracted from the C# implementation. Compilation and execution remain unverified.

## Diagnosing CI Gate status

The aggregate gate now reads each `needs.<job>.result` separately and reports the job name plus GitHub Actions conclusion in `$GITHUB_STEP_SUMMARY`. A failed, cancelled or skipped dependency makes the gate fail. Open the CI Gate job summary to identify which upstream job to inspect. GitHub Actions checks are separate from legacy GitHub commit statuses; absence of legacy statuses cannot establish Actions availability or health.


## Signed unary negation

Unchecked unary minus is lowered via Z3 BVNeg, preserving fixed-width wraparound, including negation of `int.MinValue`. Checked unary minus remains unsupported and fails closed. Tests cover both cases; execution is unverified.

# Aggregate CI Gate

A new `ci-gate` job depends on `compile-all`, runtime/analyzer tests, solver tests, source-verification tests, CLI smoke and HTML tutorial smoke. It runs even when dependencies fail and only succeeds if every dependency finished `success`. This provides one required check for branch protection; the repository branch-protection setting has not been changed. A successful CI run is not yet confirmed.

# Source-verification hardening: contract arguments

The compiler-bound verifier now requires both a nonblank constant contract ID and a compile-time constant message. A dynamic message such as `GetMessage()` may invoke arbitrary C# code, which the parameter-only proof model does not execute; it must therefore be rejected rather than silently omitted. Regression tests cover dynamic messages and blank IDs. Build/CI success is not confirmed.

# Build-stabilization update — complete compilation report

GitHub Actions `compile-all` iterates over all nine SharpContracts projects, records individual pass/fail statuses to `artifacts/build/summary.log`, captures full compiler output in `artifacts/build/build.log`, and returns failure after completing the entire matrix if any build fails. Both logs are uploaded with `if: always()`. This is diagnostic infrastructure, not evidence that current code compiles.

# CI build diagnostics

The workflow now has a dedicated `compile-all` job that executes `dotnet build` for each production and test project, records the console output to a persisted log artifact, and prints `dotnet --info` for SDK diagnostics. Separate runtime, solver, verifier, CLI, and HTML checks remain. This adds diagnosability, not proof that builds pass.

# CI troubleshooting — test evidence

The three .NET test jobs now emit TRX reports using `dotnet test --logger trx` and retain their results in GitHub Actions artifacts for 14 days. The artifact upload step uses `if: always()` to collect results following a failed test step. If restoration or compilation stops before test discovery, no TRX file is generated; the job console log remains the source for that failure. This environment has no installed .NET SDK or network access, so the CI status is not independently verified.

# Milestone 18 — CI proof-result contract

The CLI smoke test now validates the actual JSON protocol, not merely process exit status. It executes the proven byte example and the signed-overflow counterexample using the .NET CLI, checks schema version 1, outcome category, nonempty checks and exit codes. The Python test is in `scripts/check_cli_report.py` and runs in GitHub Actions. The local environment has no .NET SDK; a passing CI run remains unconfirmed.

# Milestone 17 — structured proof reports

Use `ProofReport.From(BoundNumericVerifier.Verify(source, method))` to obtain a schema-versioned result and `ToJson()` to serialize it. Outcomes: Proved, Counterexample, Unknown, InconsistentAssumptions, Unsupported, NoContracts. CLI option `--json` emits the report. Exit codes: 0/1/3/4 respectively, with 2 for unsupported/no contracts. Machine-readable results are suitable for future CI quality gates; no green build has been confirmed.

# Milestone 16 — avoiding vacuous proofs

Z3 considers `P => Q` true whenever `P` is impossible. For developer-facing contracts that is dangerous: `Require(x > 5)` and `Require(x < 0)` jointly admit no values, so even an arbitrary postcondition follows vacuously. The compiler-bound verifier now checks precondition satisfiability before verifying the first postcondition and rejects UNSAT or UNKNOWN feasibility. Regression tests cover contradictory and feasible preconditions.

The mathematical implication remains formally valid, but SharpContracts reports an inconsistent specification rather than a useful program proof. Builds and CI results remain unverified.

# Milestone 15 — solver timeout and numeric-model guards

BoundNumericVerifier now limits each Z3 query to 5 seconds. Verification results can remain Unknown when Z3 times out; Unknown is not proof. Numeric conversion and comparison paths reject unsupported representations rather than silently lowering decimal, floating-point, or custom operator semantics to integer bit-vectors. Negative regression tests cover decimal and custom overloaded operators. Compilation and CI have not been confirmed.

# Milestone 14: UInt64 literal correctness

The bound numeric verifier previously converted integer literals through signed Int64 before creating Z3 bit vectors. This fails for UInt64 literals above long.MaxValue. Lowering now preserves the full unsigned 64-bit pattern. Two new regression tests use ulong.MaxValue (18446744073709551615UL) for proof and counterexample checks. These tests have not yet been executed successfully in CI.

# Milestone 13 — differential verification against C# execution

Added `ClrDifferentialTests` to the verification test project. Tests compare Z3 outcomes with compiled C# behavior for signed 32-bit overflow, unsigned 64-bit wraparound, exhaustive byte addition promotion (256 × 256 input pairs), and checked overflow exceptions.

These tests increase confidence in the encoded subset but are **not a proof of translator soundness**. CI still needs an observed green run. Current local execution environment lacks dotnet; verification tests were committed but not executed.

# Milestone 12 — running the real verifier

SharpContracts now includes `SharpContracts.Cli`, a small executable that reads a C# file and invokes the Roslyn-bound Z3 verifier. It reports contract IDs, source line numbers, solver status and counterexamples.

```sh
dotnet run --project SharpContracts.Cli -- examples/VerifiedByteAddition.cs Check
dotnet run --project SharpContracts.Cli -- examples/OverflowCounterexample.cs Check
```

**Exit codes:** 0 means one or more contracts were proved with no verification errors, 1 means a counterexample or unknown solver result, and 2 means unsupported input, compilation failure, or no contracts.

These examples exercise the actual .NET solver rather than the browser's illustrative JavaScript. CI includes a job that verifies both expected success and expected counterexample outcomes. Execution of the workflow has not yet been confirmed.

# SharpContracts — Introduction (Milestone 11)

**New to contracts or Z3?** Start with the [five-chapter interactive tutorial](../tutorial/index.html), which covers runtime checks, implication proofs, counterexamples, integer overflow, type promotion, and local .NET execution. Browser examples are educational simulations; the solver runs in the C# implementation.


## Compiler-bound Z3 numeric verifier

`BoundNumericVerifier.Verify(source, methodName)` is a new **Roslyn IOperation-to-Z3** proof prototype for parameter-only, synchronous `void` methods containing `Contract.Require` and `Contract.Ensure` calls. It binds C# source against reference assemblies and obtains operand/result types and conversion operations from the compiler. Its lowering creates sized bit-vectors, sign/zero extensions for numeric conversions, and signed versus unsigned comparisons.

Unlike the earlier hardcoded `long` verifier, this can distinguish `byte + byte` (promoted to signed 32-bit), `int + int` (32-bit signed) and `ulong + ulong` (64-bit unsigned). Verification tests include proofs and overflow counterexamples. The verifier rejects checked arithmetic, user-defined operators, unsupported conversions, local mutation, control-flow statements, and other expressions rather than claiming a proof.

**Validation status:** Source/tests are committed but have not been compiled or executed in this environment; CI status is not confirmed. No claim is made that the implementation is fully sound for all C# inputs. C# implicit conversion and overload corner cases, short-circuit exception semantics, and integration with a control-flow graph remain open.

## Previous milestones

# SharpContracts — Introduction (Milestone 10)

SharpContracts is a C# Design by Contract toolkit inspired by Eiffel. Preconditions declare what callers must supply, postconditions declare what a method guarantees after a successful return, and invariants declare states that must hold at explicitly checked boundaries.

## Milestone 10: branch-sensitive verification and fail-closed reporting

The verifier now explores both sides of supported `if/else` statements, maintaining independent symbolic variable assignments and branch assumptions. An `Ensure` reached on multiple paths produces a check for each path. The `AllProved` summary requires every check to be proved and no unsupported-code errors.

Unsupported statements, C# parsing errors, or a path explosion above 256 paths invalidate the entire method report; partial proof results are discarded. Regression tests cover a valid clamp, a failing else branch, malformed source, and an unsupported loop after a contract check.

This remains a **mathematical integer** proof prototype, not sound CLR `long` overflow verification. It also does not yet perform full semantic compilation, analyze arbitrary method calls, or support method return values.

## Roslyn-to-Z3 source verification — Milestone 10

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
