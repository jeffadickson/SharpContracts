using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SharpContracts.Verification;
using Xunit;

namespace SharpContracts.Verification.Tests;

public sealed class NumericTypeDescriptorTests
{
    private const string Source = """
        public readonly record struct Voltage(int Millivolts)
        {
            public static Voltage operator +(Voltage a, Voltage b)
                => new(checked(a.Millivolts + b.Millivolts));
        }
        public class Usage {
            public long Add(long a, long b) => a + b;
            public Voltage AddVoltage(Voltage a, Voltage b) => a + b;
        }
        """;

    [Fact]
    public void PrimitiveWidthsFollowRoslynSemanticTypes()
    {
        var compilation = MakeCompilation();
        var type = compilation.GetSpecialType(SpecialType.System_Int64);
        var result = NumericTypeDescriptor.FromSymbol(type);
        Assert.Equal(NumericModelKind.SignedBitVector, result.Kind);
        Assert.Equal(64, result.Width);
        Assert.False(result.RequiresOperatorModel);
        var small = NumericTypeDescriptor.FromSymbol(compilation.GetSpecialType(SpecialType.System_Byte));
        Assert.Equal(NumericModelKind.UnsignedBitVector, small.Kind);
        Assert.Equal(8, small.Width);
    }

    [Fact]
    public void UserDefinedStructIsNotTreatedAsPrimitive()
    {
        var compilation = MakeCompilation();
        var type = compilation.GetTypeByMetadataName("Voltage");
        Assert.NotNull(type);
        var descriptor = NumericTypeDescriptor.FromSymbol(type!);
        Assert.Equal(NumericModelKind.Custom, descriptor.Kind);
        Assert.True(descriptor.RequiresOperatorModel);
        Assert.Null(descriptor.Width);
    }

    [Fact]
    public void OverloadedOperatorRequiresVerifiedModel()
    {
        var compilation = MakeCompilation();
        var type = compilation.GetTypeByMetadataName("Voltage");
        Assert.NotNull(type);
        var add = type!.GetMembers("op_Addition");
        var symbol = Assert.IsAssignableFrom<IMethodSymbol>(Assert.Single(add));
        Assert.False(NumericOperatorPolicy.CanLowerBuiltinArithmetic(symbol));
        Assert.Throws<NotSupportedException>(() => NumericOperatorPolicy.RequireSupportedOperator(symbol));
    }

    private static CSharpCompilation MakeCompilation() =>
        CSharpCompilation.Create("ModelTests",
            new[] { CSharpSyntaxTree.ParseText(Source) },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.IsExternalInit).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
}
