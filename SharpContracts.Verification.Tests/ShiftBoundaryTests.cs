using FsCheck.Xunit;
using Microsoft.Z3;
using SharpContracts.Solver;
using SharpContracts.Symbolic;
using Xunit;

namespace SharpContracts.Verification.Tests;

/// <summary>SC-090: differential CLR boundaries and backend type rejection, independent of source fixtures.</summary>
public sealed class ShiftBoundaryTests
{
    [Theory]
    [InlineData(32,true)] [InlineData(32,false)]
    [InlineData(64,true)] [InlineData(64,false)]
    public void PromotedWidthsMatchClrAtCountAndValueBoundaries(int width,bool signed)
    {
        using var context=new Context();
        var encoder=new ClrExpressionEncoder(context);
        foreach (var value in new ulong[]{0,1,0x80000000UL,0x8000000000000000UL,ulong.MaxValue})
        foreach (var count in new[]{int.MinValue,-65,-64,-33,-32,-1,0,1,31,32,33,63,64,65,int.MaxValue})
            Assert.True(Matches(encoder,value,count,width,signed),$"width={width}, signed={signed}, value={value}, count={count}");
    }

    /// <summary>Compares exact result bits: Int32 values are encoded in their low 32 bits, not sign-extended UInt64.</summary>
    internal static bool Matches(ClrExpressionEncoder encoder,ulong bits,int count,int width,bool signed)
    {
        uint u=unchecked((uint)bits);
        int i=unchecked((int)u);
        long l=unchecked((long)bits);
        ulong[] expected=(width,signed) switch
        {
            (32,true)=>new[]{(ulong)unchecked((uint)(i<<count)),(ulong)unchecked((uint)(i>>count)),(ulong)unchecked((uint)(i>>>count))},
            (32,false)=>new[]{(ulong)(u<<count),(ulong)(u>>count),(ulong)(u>>>count)},
            (64,true)=>new[]{unchecked((ulong)(l<<count)),unchecked((ulong)(l>>count)),unchecked((ulong)(l>>>count))},
            _=>new[]{bits<<count,bits>>count,bits>>>count}
        };
        var operations=new[]{ClrBinaryOperator.ShiftLeft,ClrBinaryOperator.ShiftRight,ClrBinaryOperator.UnsignedShiftRight};
        var type=ClrScalarType.Integral(width,signed);
        for (int index=0;index<operations.Length;index++)
        {
            var expression=new ClrBinary(operations[index],new ClrConstant(width==32?u:bits,type),
                new ClrConstant(unchecked((uint)count),ClrScalarType.Integral(32,true)),type);
            if (((BitVecNum)encoder.Encode(expression).Simplify()).UInt64!=expected[index]) return false;
        }
        return true;
    }

    [Theory]
    [InlineData(8,32)] [InlineData(16,32)] [InlineData(32,64)]
    public void UnpromotedLeftOrWrongCountWidthsFailClosed(int width,int countWidth)
    {
        using var context=new Context();
        var type=ClrScalarType.Integral(width,false);
        var expression=new ClrBinary(ClrBinaryOperator.ShiftLeft,new ClrConstant(1,type),
            new ClrConstant(1,ClrScalarType.Integral(countWidth,true)),type);
        Assert.Throws<ArgumentException>(()=>new ClrExpressionEncoder(context).Encode(expression));
    }

    [Fact]
    public void UnsignedCountAndMismatchedResultTypesFailClosed()
    {
        using var context=new Context();
        var u32=ClrScalarType.Integral(32,false);
        var i32=ClrScalarType.Integral(32,true);
        var encoder=new ClrExpressionEncoder(context);
        Assert.Throws<ArgumentException>(()=>encoder.Encode(new ClrBinary(ClrBinaryOperator.ShiftLeft,
            new ClrConstant(1,u32),new ClrConstant(1,u32),u32)));
        Assert.Throws<ArgumentException>(()=>encoder.Encode(new ClrBinary(ClrBinaryOperator.ShiftLeft,
            new ClrConstant(1,u32),new ClrConstant(1,i32),i32)));
    }
}

/// <summary>Seeded FsCheck inputs are shrunk automatically if any encoded shift differs from compiled CLR arithmetic.</summary>
public sealed class ShiftPropertyTests
{
    [Property(MaxTest=256, Replay="20261024,1357911")]
    public bool AllPromotedShiftFormsMatchClr(ulong value,int count)
    {
        using var context=new Context();
        var encoder=new ClrExpressionEncoder(context);
        return ShiftBoundaryTests.Matches(encoder,value,count,32,true) &&
            ShiftBoundaryTests.Matches(encoder,value,count,32,false) &&
            ShiftBoundaryTests.Matches(encoder,value,count,64,true) &&
            ShiftBoundaryTests.Matches(encoder,value,count,64,false);
    }
}
