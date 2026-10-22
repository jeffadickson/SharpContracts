## Compiler-bound IR integration (2026-10-22)

NumericTypeDescriptor now feeds the common ClrOperationLowerer used by the numeric and inline entry points. Built-in widths and signedness reach the typed symbolic AST and common Z3 encoder. Custom operators, conversions, nullable lifting and generic math are still rejected; the legacy BV64 engine remains separate. This closes primitive-expression integration, not custom-type verification.

# C# Custom Numeric Types — SharpContracts M9

SharpContracts must verify the **actual semantics** of custom types, rather than assume that overloaded arithmetic behaves like built-in arithmetic.

## Example

```csharp
public readonly record struct Voltage(int Millivolts)
{
    public static Voltage operator +(Voltage a, Voltage b)
        => new(checked(a.Millivolts + b.Millivolts));
}
```

Here, `Voltage + Voltage` invokes a user-written method with checked 32-bit arithmetic. Treating it as a 64-bit addition or unbounded mathematical integer addition would be wrong.

## Implemented M9 foundation

`NumericTypeDescriptor.FromSymbol(ITypeSymbol)` classifies Roslyn semantic types into signed/unsigned bit-vectors, floating-point, decimal, custom and unsupported categories. It recognizes native primitive widths (8, 16, 32, 64), differentiates signedness, and does not accidentally classify a user-defined struct as a primitive. `NumericOperatorPolicy.RequireSupportedOperator` explicitly refuses a custom operator until it has an analyzed body or separately verified/trusted specification.

This metadata is **not yet connected to the old BV64 lowering engine**, and does not prove user-defined operator semantics. Custom-type support is a staged architecture effort, not a completed verifier.

## Required behavior

1. Query Roslyn semantic symbols and bound operations (`IOperation`) to resolve numeric promotion, conversion and operator dispatch.
2. Lower built-in arithmetic at the actual promoted expression width and signedness, including checked context.
3. Model immutable structs as typed records with fields and invariants.
4. Verify user-defined operator method bodies against explicit specifications; reject unmodeled bodies.
5. Support user-defined implicit and explicit conversions, including throwing conversions.
6. Treat generic math interfaces such as `INumber<TSelf>` as operator availability, not guarantees of associativity or overflow behavior.
7. Eventually handle classes, aliasing, mutation, frame conditions and exceptional postconditions.

## Soundness rule

A result may be marked **Proved** only if the actual bound implementation is modeled under its relevant execution semantics. Unmodeled operations must result in Unsupported (or an explicit trusted assumption disclosed in the report), never be silently assumed mathematically correct.
