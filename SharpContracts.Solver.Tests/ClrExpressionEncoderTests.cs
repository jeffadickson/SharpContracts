using Microsoft.Z3;
using SharpContracts.Symbolic;
using Xunit;

namespace SharpContracts.Solver.Tests;

public sealed class ClrExpressionEncoderTests
{
    [Theory]
    [InlineData(8, true)] [InlineData(8, false)]
    [InlineData(16, true)] [InlineData(16, false)]
    [InlineData(32, true)] [InlineData(32, false)]
    [InlineData(64, true)] [InlineData(64, false)]
    public void TypedAdditionHasDeclaredWidthAndWraps(int width, bool signed)
    {
        var type = ClrScalarType.Integral(width, signed);
        ulong maximum = width == 64 ? ulong.MaxValue : (1UL << width) - 1;
        var expression = new ClrBinary(ClrBinaryOperator.Add,
            new ClrConstant(maximum, type), new ClrConstant(1, type), type);
        using var context = new Context();
        var result = new ClrExpressionEncoder(context).Encode(expression).Simplify();
        Assert.Equal(0UL, Assert.IsType<BitVecNum>(result).UInt64);
        Assert.Equal(SymbolicType.BitVector, expression.Type);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(24)] [InlineData(128)]
    public void UnsupportedWidthsCannotEnterIr(int width)
        => Assert.Throws<ArgumentOutOfRangeException>(() => ClrScalarType.Integral(width, false));

    [Theory]
    [InlineData("operand-type")]
    [InlineData("result-type")]
    [InlineData("overflowing-constant")]
    [InlineData("invalid-bool")]
    [InlineData("bool-numeric-conversion")]
    [InlineData("checked-conversion")]
    [InlineData("checked-binary")]
    [InlineData("checked-unary")]
    [InlineData("signed-divide")]
    [InlineData("zero-divide")]
    [InlineData("variable-divide")]
    [InlineData("invalid-unary")]
    public void MalformedOrExceptionalIrNeverProducesAFormula(string scenario)
    {
        var u32 = ClrScalarType.Integral(32, false);
        var i32 = ClrScalarType.Integral(32, true);
        var u64 = ClrScalarType.Integral(64, false);
        var one = new ClrConstant(1, u32);
        ClrExpression expression = scenario switch
        {
            "operand-type" => new ClrBinary(ClrBinaryOperator.Add, one, new ClrConstant(1, u64), u32),
            "result-type" => new ClrBinary(ClrBinaryOperator.Equal, one, one, u32),
            "overflowing-constant" => new ClrConstant(1UL << 32, u32),
            "invalid-bool" => new ClrConstant(2, ClrScalarType.Boolean),
            "bool-numeric-conversion" => new ClrConvert(one, ClrScalarType.Boolean),
            "checked-conversion" => new ClrConvert(one, u64, true),
            "checked-binary" => new ClrBinary(ClrBinaryOperator.Add, one, one, u32, true),
            "checked-unary" => new ClrUnary(ClrUnaryOperator.Negate, one, u32, true),
            "signed-divide" => new ClrBinary(ClrBinaryOperator.Divide,
                new ClrConstant(1, i32), new ClrConstant(1, i32), i32),
            "zero-divide" => new ClrBinary(ClrBinaryOperator.Divide, one, new ClrConstant(0, u32), u32),
            "variable-divide" => new ClrBinary(ClrBinaryOperator.Divide, one, new ClrParameter("d", u32), u32),
            "invalid-unary" => new ClrUnary(ClrUnaryOperator.LogicalNot, one, u32),
            _ => throw new InvalidOperationException(scenario)
        };
        using var context = new Context();
        var exception = Record.Exception(() => new ClrExpressionEncoder(context).Encode(expression));
        Assert.True(exception is ArgumentException or NotSupportedException, exception?.ToString());
    }

    [Fact]
    public void ConflictingSymbolTypesFailClosed()
    {
        using var context = new Context();
        var encoder = new ClrExpressionEncoder(context);
        encoder.Encode(new ClrParameter("x", ClrScalarType.Integral(32, true)));
        Assert.Throws<ArgumentException>(() => encoder.Encode(
            new ClrParameter("x", ClrScalarType.Integral(32, false))));
    }

    [Fact]
    public void OldMathematicalBackendCannotSilentlyConsumeClrNodes()
    {
        var scalar = ClrScalarType.Integral(32, true);
        var x = new ClrParameter("x", scalar);
        var predicate = new ClrBinary(ClrBinaryOperator.Equal, x, x, ClrScalarType.Boolean);
        Assert.Throws<NotSupportedException>(() => Z3Verifier.Verify(Array.Empty<ContractExpr>(), predicate));
        Assert.Throws<NotSupportedException>(() => SmtLibPrinter.Print(predicate));
        Assert.Throws<NotSupportedException>(() => ContractExprEvaluator.Check(predicate, new Dictionary<string, object>()));
    }
}
