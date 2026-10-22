# Current inline verification implementation - 2026-10-22

Both compiler-bound entry points now lower Roslyn `IOperation` into `ClrExpression`, a typed extension of the original `ContractExpr` AST. `ClrScalarType` preserves Boolean identity or 8/16/32/64-bit integral width and signedness. Nodes carry source spans; conversions, unary/binary operations and overflow context are explicit. `ClrExpressionEncoder` validates types before producing Z3 expressions, and `ProofQuery` provides the common feasibility/status/model policy.

Unchecked conversions narrow by extraction and widen by source-appropriate sign/zero extension. C# promotions come from Roslyn, including byte addition and mixed uint/int addition to long. Multiplication and bitwise operations wrap at the promoted width. Division accepts only unsigned operands and a compile-time nonzero unsigned divisor. Checked operations, signed/variable division, custom operators, lifted nullable operations, heap access and unsupported statements fail closed.

Both entry points reject source-defined lookalikes of the runtime contract API. Initial inconsistent requirements produce exit 4. A later contract unreachable because an earlier check always throws is UNKNOWN, while the earlier counterexample is retained. Feasibility UNKNOWN is a check result with a reason and cannot yield overall Proved. Counterexample strings preserve signed parameter values and support executable CLR replay.

The [100-case iteration ledger](ITERATION_LOG.md) records the source-to-Z3/CLR corpus, build cadence, reviews and limitations. Full control-flow and legacy-engine migration remain open. Older milestone notes below are historical; their unverified-status statements describe earlier revisions.

## Current supported-language matrix

| Capability | Numeric entry point | Inline entry point |
|---|---|---|
| Method | Unique synchronous void block method | Unique synchronous uint block method with runtime VerifyAttribute |
| Parameters | Ordinary integral 8/16/32/64-bit parameters, including char | Ordinary uint parameters |
| Contract calls | Runtime Require/Ensure | Runtime Require/Assert/Ensure |
| Locals and return | No local statements or return values | One initialized uint local per declaration; one final uint return |
| Expressions | Shared typed integral/Boolean model | Same expression model, including compiler conversions in predicates |
| Total operators | +, -, *, comparisons, unary + / - / ~ / !, pure Boolean and bitwise AND/OR/XOR | Same |
| Division | Unsigned operands with a nonzero compile-time unsigned divisor | Same |
| Preconditions | Before postcondition checks | Before executable statements |
| Rejection | Checked arithmetic/conversions, signed/variable division, shifts, remainder, nullable/custom operators, side effects, heap state and unsupported statements | Same; assignments/branches/loops remain unsupported |
| Evidence | Source-derived Z3 results plus signed/unsigned CLR witness replay | Same, plus exact flagship-source regression tests |

The expression core is shared; the statement walkers and method-selection conventions remain separate. A successful check is assumed only for normal runtime fallthrough. An unreachable later check is Unknown, and an earlier counterexample is retained. Symbolic IR nodes extend ContractExpr; old mathematical backends reject them rather than treating CLR bit vectors as unbounded numbers. Serialization and cross-project VeloFormal integration remain future work.

## Counterexample replay and report hardening — 2026-10-20

The first stabilization commit `360efa09eca8679ea23e8c3f56663b64e27705be` passed [all seven GitHub Actions jobs, including CI Gate](https://github.com/jeffadickson/SharpContracts/actions/runs/39549650831). Local regression coverage now totals 193 passing xUnit cases, zero failures/skips, including eight new tests for solver-input replay, inconsistent inline preconditions, by-reference rejection, and checked/unchecked return wrappers.

Inline counterexamples now include decimal-string `counterexampleInputs` keyed by parameter name alongside the Z3 model, contract ID and source line. Tests replay actual solver inputs against the exact compiled annotated C# examples. Inline feasibility distinguishes inconsistent assumptions (exit 4) from solver UNKNOWN (exit 3); unsupported translation still clears every partial proof. By-reference parameters/returns are explicitly unsupported. CLI validation executes all six flagship source/method combinations plus the two numeric examples. Broader typed-IR consolidation, branches, assignments and modeled checked exceptions remain open in Issue #2.
## Executed stabilization baseline — 2026-10-20

Windows x64, .NET SDK 8.0.132: all nine projects compile; 185 xUnit tests pass (16 runtime, 6 solver, 163 verification), zero failures/skips. The archived corpus separately passes 100 vectors plus its integrity test. Native Z3 4.12.2 executes; the three corrected annotated examples prove their four obligations and all three defective examples yield counterexamples. CLI JSON/exit-code validation and embedded HTML script syntax checks pass.

Fixed Roslyn metadata-reference overload inference and semantic operation lookup for checked/unchecked initializer and return wrappers. `SharpContracts.sln` supports root restore/build/test. Linux CI now selects the matching upstream Z3 4.12.2 native library using a pinned SHA-256 because the NuGet package ships no Linux library. Linux execution and a green CI Gate remain pending; this local evidence does not establish general C# soundness or architecture consolidation. The symbolic expression model remains in `SharpContracts.Runtime/Symbolic`.
# Inline Contracts — Authoring and Verification Design

Version: 0.1 (annotated UInt32 vertical slice). Status: implemented in source, compilation/CI not yet independently confirmed.

## Authoring

Use `[Verify]` on a supported method. Express contracts in ordinary, compiler-checked C#:

```csharp
using SharpContracts;

[Verify]
public static uint Midpoint(uint low, uint high)
{
    Contract.Require(low <= high, "MID-PRE", "Valid interval");
    uint midpoint = unchecked(low + (high - low) / 2u);
    Contract.Assert(midpoint >= low, "MID-LOW", "Lower boundary");
    Contract.Ensure(midpoint <= high, "MID-HIGH", "Upper boundary");
    return midpoint;
}
```

The public API is:
- `[Verify]`: opt-in verification attribute; does **not** cause automatic runtime weaving.
- `Contract.Require(bool, string, string)`: checked runtime precondition and solver assumption.
- `Contract.Assert(bool, string, string)`: checked runtime assertion and proof obligation at that point.
- `Contract.Ensure(bool, string, string)`: checked runtime assertion and proof obligation at that point. It is **not yet** a method-level postcondition that is automatically executed at every exit.
- `Contract.Old<T>`: preexisting runtime helper, not supported in this inline source verifier.
- `Contract.Result<T>`: not implemented; returning expressions are analyzed only to confirm supported syntax.

## Verification algorithm

`InlineContractVerifier.Verify(source, methodName)` constructs a Roslyn compilation of the source and resolves the actual `VerifyAttribute` and `Contract` calls. For the current UInt32 subset, parameters become Z3 BV32 variables, local initializers become symbolic terms, `Require` adds assumptions, and `Assert`/`Ensure` add an independent counterexample query (`assumptions && !predicate`). UNSAT is proved, SAT is counterexample, UNKNOWN is not proved. Satisfiability of current assumptions is checked before each proof query to reject vacuous successes.

The implementation is *not* a custom parser of C# expression strings. Z3 terms are derived from Roslyn `IOperation` nodes, preserving unsigned 32-bit wraparound for supported operations. Unmodeled statements cause an error and discard all partial proof results.

## Exact supported subset

- A uniquely named, synchronous `uint`-returning method annotated with `[Verify]`.
- Ordinary `uint` parameters and initialized `uint` locals; one variable per declaration.
- `uint` constants, Boolean comparisons, `+`, `-`, unsigned `/` with a known nonzero `uint` constant, equality comparisons.
- Unchecked arithmetic, with explicit `unchecked(...)` supported.
- `Require` before local execution; `Assert` and `Ensure` after/between supported local declarations; each with a nonblank compile-time string ID and compile-time string message.
- A single final `return` expression of unsigned 32-bit type.

## Unsupported and not silently proved

Branches, loops, assignments to existing locals, method calls outside the supported contract API, checked overflow, arbitrary division, custom operators, user-defined numeric types, references, fields, async, exceptions, heap mutation, `Old`, and method-level `Result` contracts. Generic source-to-Z3 verification of arbitrary C# is not yet implemented.

The runtime `Contract.Assert` uses a `ContractViolationException` with `Postcondition` kind. This is a pragmatic choice for the current exception model; a distinct assertion-violation category remains an enhancement.

## Using the CLI

```sh
dotnet run --project SharpContracts.Cli -- --inline examples/BinarySearchMidpoint.cs Correct
dotnet run --project SharpContracts.Cli -- --inline --json examples/BinarySearchMidpoint.cs Buggy
```

Expected: Correct exits 0 with both obligations proved. Buggy exits 1 with a midpoint-below-low counterexample. CI has specific gates for these two commands and xUnit tests that read the **same annotated source file**.

## Proof semantics

The proof is a statement about the supported checked source subset and its BV32 encoding, not about all possible C# executions. This first milestone deliberately keeps the translated subset small so that correctness can be examined and tested. Z3's native implementation is external and test execution has not yet been independently confirmed.

## Next steps

First establish passing CI and verify this source-based proof result. Then expand to sound method-level Ensures at all exits, CFG/SSA for assignments and branches, checked arithmetic including exceptions, custom types, and proof-result annotations in IDE builds. Do not infer these features exist from the example syntax.
