using System.Globalization;
using FsCheck.Xunit;

namespace SharpContracts.Verification.Tests;

/// <summary>Seeded randomized and malformed-source checks supplement the SC-089 regression witnesses.</summary>
public sealed class AssignmentPropertyTests
{
    /// <summary>Checks symbolic snapshots against compiled unchecked CLR arithmetic at reproducible inputs.</summary>
    [Property(MaxTest=32, Replay="12345,67891")]
    public bool AddSubtractRoundTrip(uint value, uint delta)
    {
        string v=value.ToString(CultureInfo.InvariantCulture), d=delta.ToString(CultureInfo.InvariantCulture);
        var source=LocalAssignmentTests.Source($$"""
            Contract.Require(x=={{v}}u,"A","generated input");
            uint y=x;
            y=unchecked(y+{{d}}u);
            y=unchecked(y-{{d}}u);
            Contract.Ensure(y==x,"P","roundtrip");
            return y;
            """);
        return InlineContractVerifier.Verify(source,"M").AllProved && unchecked(value+delta-delta)==value;
    }

    /// <summary>Bounded source fuzzing must never turn an unmodeled throwing suffix into a proof.</summary>
    [Property(MaxTest=32, Replay="54321,98765")]
    public bool MalformedSourceNeverProves(byte[] input)
    {
        var text=new string(input.Take(24).Select(b=>(char)(32+b%95)).ToArray());
        var source=LocalAssignmentTests.Source(text+"\nthrow new System.Exception();");
        return !InlineContractVerifier.Verify(source,"M").AllProved;
    }
}
