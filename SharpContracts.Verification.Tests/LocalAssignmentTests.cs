using SharpContracts.Solver;
using Xunit;

namespace SharpContracts.Verification.Tests;

/// <summary>SC-089: immutable symbolic updates for ordinary UInt32 local storage.</summary>
public sealed class LocalAssignmentTests
{
    /// <summary>Creates a complete source method so Roslyn resolves storage and conversion semantics.</summary>
    public static string Source(string body) =>
        "using SharpContracts; public static class Cases { [Verify] public static uint M(uint x) { " + body + " } }";

    [Theory]
    [InlineData("uint y=x; y=0u; Contract.Ensure(y==0u,\"P\",\"reset\"); return y;")]
    [InlineData("uint y=x; uint old=y; y=unchecked(y+1u); Contract.Ensure(old==x,\"P\",\"snapshot\"); return y;")]
    [InlineData("uint y=x; y=unchecked(y+1u); y=unchecked(y-1u); Contract.Ensure(y==x,\"P\",\"roundtrip\"); return y;")]
    public void LocalUpdatesPreserveSsaSnapshots(string body)
    {
        var result=InlineContractVerifier.Verify(Source(body),"M");
        Assert.Empty(result.Errors);
        Assert.True(result.AllProved);
    }

    [Fact]
    public void UpdatedLocalWraparoundHasOriginalInputWitness()
    {
        var source=Source("uint y=x; y=unchecked(y+1u); Contract.Ensure(y>x,\"P\",\"increase\"); return y;");
        var result=InlineContractVerifier.Verify(source,"M");
        Assert.Empty(result.Errors);
        var check=Assert.Single(result.Checks);
        Assert.Equal(VerificationStatus.Counterexample,check.Status);
        Assert.Equal(uint.MaxValue.ToString(),check.CounterexampleInputs!["x"]);
        // Execute the exact annotated source with the solver witness, rather than only its arithmetic.
        RequirementIterationTests.Replay(source,ProofReport.From(result));
    }

    [Theory]
    [InlineData("x=0u; Contract.Ensure(x==0u,\"P\",\"parameter\"); return x;")]
    [InlineData("ref uint y=ref x; y=0u; Contract.Ensure(x==0u,\"P\",\"alias\"); return y;")]
    [InlineData("uint y=x; y+=1u; Contract.Ensure(y>x,\"P\",\"compound\"); return y;")]
    [InlineData("uint y=x; y=checked(y+1u); Contract.Ensure(y>x,\"P\",\"exception\"); return y;")]
    public void UnmodeledStorageOrExceptionsClearPartialResults(string body)
    {
        var result=InlineContractVerifier.Verify(Source(body),"M");
        Assert.NotEmpty(result.Errors);
        Assert.Empty(result.Checks);
    }
}
