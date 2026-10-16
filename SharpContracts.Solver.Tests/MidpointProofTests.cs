using Microsoft.Z3;
using Xunit;

namespace SharpContracts.Solver.Tests;

/// <summary>Full-domain BV32 proof of the binary-search midpoint range invariant.</summary>
public sealed class MidpointProofTests
{
    private const uint Low = 3_000_000_000u;
    private const uint High = 4_000_000_000u;

    private static uint Buggy(uint low, uint high) => unchecked((low + high) / 2u);
    private static uint Correct(uint low, uint high) => unchecked(low + (high - low) / 2u);

    [Fact]
    public void NormalInputsHideBugButLargeInputsExposeIt()
    {
        Assert.Equal(15u, Buggy(10u, 20u));
        Assert.Equal(15u, Correct(10u, 20u));
        Assert.Equal(352_516_352u, Buggy(Low, High));
        Assert.Equal(3_500_000_000u, Correct(Low, High));
        Assert.True(Buggy(Low, High) < Low);
    }

    [Fact]
    public void Z3FindsBadMidpointAndProvesCorrectedMidpointForAllUInt32()
    {
        using var ctx = new Context();
        using var solver = ctx.MkSolver();
        using var timeout = ctx.MkParams();
        timeout.Add("timeout", 10_000u);
        solver.Parameters = timeout;

        var low = ctx.MkBVConst("low", 32);
        var high = ctx.MkBVConst("high", 32);
        var two = ctx.MkBV(2u, 32);
        var valid = ctx.MkBVULE(low, high);

        BitVecExpr buggy = ctx.MkBVUDiv(ctx.MkBVAdd(low, high), two);
        BitVecExpr correct = ctx.MkBVAdd(
            low, ctx.MkBVUDiv(ctx.MkBVSub(high, low), two));

        BoolExpr inBounds(BitVecExpr mid) => ctx.MkAnd(
            ctx.MkBVULE(low, mid), ctx.MkBVULE(mid, high));

        solver.Assert(valid);
        solver.Assert(ctx.MkNot(inBounds(buggy)));
        Assert.Equal(Status.SATISFIABLE, solver.Check());

        // The counterexample is real: check a concrete boundary point in the same model.
        solver.Push();
        solver.Assert(ctx.MkEq(low, ctx.MkBV(Low, 32)));
        solver.Assert(ctx.MkEq(high, ctx.MkBV(High, 32)));
        Assert.Equal(Status.SATISFIABLE, solver.Check());
        solver.Pop();

        solver.Reset();
        solver.Assert(valid);
        solver.Assert(ctx.MkNot(inBounds(correct)));
        Assert.Equal(Status.UNSATISFIABLE, solver.Check());
    }

    [Fact]
    public void CorrectVersionHandlesBoundaryCases()
    {
        Assert.Equal(0u, Correct(0u, 0u));
        Assert.Equal(uint.MaxValue, Correct(uint.MaxValue, uint.MaxValue));
        Assert.Equal(2_147_483_647u, Correct(0u, uint.MaxValue));
        Assert.Equal(3_500_000_000u, Correct(Low, High));
    }
}
