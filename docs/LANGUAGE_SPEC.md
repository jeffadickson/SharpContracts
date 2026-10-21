# SharpContracts Language Specification — Draft v0.1

**Status:** Design baseline; the supported verification subset is narrower than the language described here. This file is tracked in Git and must evolve through reviewed changes.

## Relationship to VeloFormal

SharpContracts' primary contract authoring language is **ordinary C# expressions embedded in C# source**. It is not a separate parser-based DSL. The independently designed, Eiffel-inspired **VeloFormal Contracts Language** (`.vfc`) remains in the [VeloFormal repository](https://github.com/jeffadickson/VeloFormal/blob/main/docs/contract-language-v0.2.md). Its `require`, `ensure`, `invariant`, `cover`, `after N cycles`, and `within A..B cycles` constructs have RTL/temporal semantics and are **not** part of the SharpContracts C# syntax. Neither language replaces the other.

The proposed common denominator is a typed symbolic intermediate representation for expressions, assumptions, guarantees, and source locations. Sharing an IR must not erase the differences between CLR and clocked hardware semantics. A stable cross-project IR is **planned**, not implemented.

## Canonical C# surface

```csharp
using SharpContracts;

public static class Midpoint
{
    [Verify]
    public static uint Correct(uint low, uint high)
    {
        Contract.Require(low <= high, "MID-PRE", "Valid interval");
        uint midpoint = unchecked(low + (high - low) / 2u);
        Contract.Assert(midpoint >= low, "MID-LOW", "Lower bound");
        Contract.Ensure(midpoint <= high, "MID-HIGH", "Upper bound");
        return midpoint;
    }
}
```

The syntax of predicates is C# itself, checked and bound by Roslyn, not quoted strings. Attributes configure verification; they do not encode mathematical expressions as strings.

### Contract declarations

| Construct | Meaning | Current implementation |
|---|---|---|
| `[Verify]` | Opt method into source verification | Present; explicit CLI `--inline` rather than automatic whole-build discovery |
| `Contract.Require(p, id, message)` | Caller precondition; failure throws at runtime, assumption in static verification | Present in restricted subset |
| `Contract.Assert(p, id, message)` | Assertion at the current execution point | Present in restricted subset |
| `Contract.Ensure(p, id, message)` | Assertion at its current execution point; intended method-postcondition semantics require a future separate specification | Present at call sites only |
| `Contract.Invariant(p, id, message)` | Runtime invariant check at explicit boundary | Runtime only; no general static object-invariant proof |
| `Contract.Old(value)` | Runtime value copy | Present; not a deep snapshot, not yet supported by inline source verification |
| `Contract.Result<T>()` | Proposed reference to a method result | **Not implemented** |

### Evaluation and proof semantics

1. Runtime `Require`, `Assert`, `Ensure` and `Invariant` evaluate ordinary C# arguments and throw `ContractViolationException` when false. Contract expressions must not be presumed pure merely because they appear in a contract call.
2. Formal verification must resolve calls and expressions using Roslyn symbols/operations. Unmodeled side effects, exceptions, casts, branches or method dispatch must fail closed, never silently become assumptions.
3. Preconditions constrain the set of allowed inputs; their feasibility must be checked. An impossible precondition set must be reported as inconsistent, not a useful proof.
4. A verification obligation asks whether assumptions and the **negation** of a property are satisfiable. UNSAT proves the property **within the encoded model**; SAT yields a counterexample; UNKNOWN is not a proof.
5. Proof results must distinguish Proved, Counterexample, Unknown, InconsistentAssumptions, Unsupported and NoContracts, with source locations and stable contract IDs.
6. C# integer widths, signedness, promotions, checked/unchecked arithmetic, division exceptions, and user-defined operators are semantic obligations. The verifier must not use unbounded mathematical integers when claiming CLR bit-accurate results.
7. A postcondition on every normal/exceptional exit, frame conditions, contract inheritance, generic math, async and mutable heap reasoning are future features, not implied by `Ensure` syntax today.

### Verified-subset baseline (experimental)

The inline source verifier currently targets uniquely selected synchronous methods returning `uint`, with `uint` parameters, initialized locals, a final return, and supported BV32 operations/comparisons. It supports only safe constant nonzero divisors and rejects unsupported control flow and checked arithmetic. It is **not** a general C# verifier. See [inline design](INLINE_CONTRACT_DESIGN.md) for the exact limitations.

### Proposed extension points (not syntax commitments)

- Attributes for proof budgets, assumptions, trusted boundaries, and diagnostics.
- Explicit method-level postconditions and exceptional guarantees.
- Type invariants, loop invariants, and frame conditions.
- Custom numeric structs and user-defined operators with proved implementations or disclosed trusted specifications.
- Typed symbolic IR serialization shared where appropriate with VeloFormal.

## Compatibility and change control

- Keep the primary authoring experience valid C#; avoid expression strings as the default contract language.
- Do not silently reinterpret existing runtime `Ensure` calls as automatic exit-time postconditions.
- Version any new IR schema and machine-readable proof reports independently.
- Update this specification, `docs/REQUIREMENTS.md`, `docs/INTRODUCTION.md`, `index.html`, and `tutorial/index.html` when the public contract surface changes.
- A committed specification is not evidence of passing CI or soundness for arbitrary programs.

## Authoritative documents

- SharpContracts inline implementation scope: [INLINE_CONTRACT_DESIGN.md](INLINE_CONTRACT_DESIGN.md)
- SharpContracts requirements: [REQUIREMENTS.md](REQUIREMENTS.md)
- VeloFormal temporal contract language: [VeloFormal v0.2 specification](https://github.com/jeffadickson/VeloFormal/blob/main/docs/contract-language-v0.2.md)

## Stabilization report details (2026-10-20)

Inline `uint` parameters and returns must be passed by value; `ref`, `in`, `out` and by-reference returns are unsupported. Schema v1 reports add optional `counterexampleInputs`: parameter names mapped to invariant decimal strings representing UInt32 solver values. SAT witnesses include all parameters, using model completion for unconstrained values. Inconsistent inline preconditions produce InconsistentAssumptions/exit 4; feasibility UNKNOWN produces Unknown/exit 3. Neither result is a proof. No authoring syntax changes are introduced.
