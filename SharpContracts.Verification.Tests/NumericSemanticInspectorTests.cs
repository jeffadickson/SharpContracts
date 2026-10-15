using System;
using System.Linq;
using SharpContracts.Verification;
using Xunit;

namespace SharpContracts.Verification.Tests;

public sealed class NumericSemanticInspectorTests
{
    [Fact]
    public void ByteAdditionPromotesToSignedInt32()
    {
        var report=NumericSemanticInspector.Inspect(
            "class C { int Sum(byte a, byte b) => a + b; }");
        Assert.Empty(report.Errors);
        var op=Assert.Single(report.Operations);
        Assert.Equal(NumericModelKind.SignedBitVector,op.Kind);
        Assert.Equal(32,op.Width);
        Assert.False(op.IsUserDefined);
    }

    [Fact]
    public void ULongAdditionUsesUnsigned64()
    {
        var report=NumericSemanticInspector.Inspect(
            "class C { ulong Sum(ulong a, ulong b) => a + b; }");
        Assert.Empty(report.Errors);
        var op=Assert.Single(report.Operations);
        Assert.Equal(NumericModelKind.UnsignedBitVector,op.Kind);
        Assert.Equal(64,op.Width);
    }

    [Fact]
    public void CheckedExpressionIsReported()
    {
        var report=NumericSemanticInspector.Inspect(
            "class C { int Sum(int a, int b) => checked(a + b); }");
        Assert.Empty(report.Errors);
        Assert.True(Assert.Single(report.Operations).IsChecked);
    }

    [Fact]
    public void CustomOperatorIsExplicitlyUnverified()
    {
        const string source = """
            struct Voltage {
                public int Millivolts;
                public static Voltage operator +(Voltage x, Voltage y)
                    => new Voltage { Millivolts = checked(x.Millivolts + y.Millivolts) };
            }
            class C { Voltage Sum(Voltage a, Voltage b) => a + b; }
            """;
        var report=NumericSemanticInspector.Inspect(source);
        Assert.Contains(report.Operations, o => o.IsUserDefined);
        Assert.Contains(report.Errors, e => e.Contains("Unverified custom operator"));
        Assert.False(report.IsSupported);
    }
}
