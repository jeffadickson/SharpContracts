using System;
using Microsoft.CodeAnalysis;

namespace SharpContracts.Verification;

/// <summary>
/// Semantic classification for proof planning. Classification is NOT verification:
/// user-defined operators still require lowering or a separately proved summary.
/// </summary>
public enum NumericModelKind { SignedBitVector, UnsignedBitVector, FloatingPoint, Decimal, Custom, Unsupported }

public sealed record NumericTypeDescriptor(
    NumericModelKind Kind, int? Width, string DisplayName, bool RequiresOperatorModel)
{
    /// <summary>Use Roslyn-bound type identity rather than source spelling.</summary>
    public static NumericTypeDescriptor FromSymbol(ITypeSymbol symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        return symbol.SpecialType switch
        {
            SpecialType.System_SByte => Primitive(NumericModelKind.SignedBitVector, 8, symbol),
            SpecialType.System_Byte => Primitive(NumericModelKind.UnsignedBitVector, 8, symbol),
            SpecialType.System_Int16 => Primitive(NumericModelKind.SignedBitVector, 16, symbol),
            SpecialType.System_UInt16 => Primitive(NumericModelKind.UnsignedBitVector, 16, symbol),
            SpecialType.System_Char => Primitive(NumericModelKind.UnsignedBitVector, 16, symbol),
            SpecialType.System_Int32 => Primitive(NumericModelKind.SignedBitVector, 32, symbol),
            SpecialType.System_UInt32 => Primitive(NumericModelKind.UnsignedBitVector, 32, symbol),
            SpecialType.System_Int64 => Primitive(NumericModelKind.SignedBitVector, 64, symbol),
            SpecialType.System_UInt64 => Primitive(NumericModelKind.UnsignedBitVector, 64, symbol),
            SpecialType.System_Single => Primitive(NumericModelKind.FloatingPoint, 32, symbol),
            SpecialType.System_Double => Primitive(NumericModelKind.FloatingPoint, 64, symbol),
            SpecialType.System_Decimal => Primitive(NumericModelKind.Decimal, 128, symbol),
            _ when symbol is INamedTypeSymbol { TypeKind: TypeKind.Struct or TypeKind.Class } s &&
                   s.SpecialType == SpecialType.None =>
                new(NumericModelKind.Custom, null, symbol.ToDisplayString(), true),
            _ => new(NumericModelKind.Unsupported, null, symbol.ToDisplayString(), true)
        };
    }

    private static NumericTypeDescriptor Primitive(NumericModelKind kind, int width, ITypeSymbol type) =>
        new(kind, width, type.ToDisplayString(), false);
}

/// <summary>
/// Describes a bound operator's trust boundary. Until the verifier supports custom operators,
/// it must reject them rather than treating their names as mathematical operations.
/// </summary>
public static class NumericOperatorPolicy
{
    public static bool CanLowerBuiltinArithmetic(IMethodSymbol? operatorSymbol) =>
        operatorSymbol is null || operatorSymbol.MethodKind == MethodKind.BuiltinOperator;

    public static void RequireSupportedOperator(IMethodSymbol? operatorSymbol)
    {
        if (!CanLowerBuiltinArithmetic(operatorSymbol))
            throw new NotSupportedException(
                "User-defined operator requires a verified implementation or an explicit trusted summary: " +
                operatorSymbol!.ToDisplayString());
    }
}
