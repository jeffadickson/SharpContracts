using System;
using System.IO;
using SharpContracts.Solver;
using SharpContracts.Verification;
using Xunit;

namespace SharpContracts.Verification.Tests;

/// <summary>Tests actual source-to-Z3 translation, without handwritten solver formulas.</summary>
public sealed class InlineMidpointTests
{
    private static string MidpointSource() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "BinarySearchMidpoint.cs"));

    [Fact]
    public void BuggyMidpointHasCounterexampleFromAnnotatedCSharp()
    {
        var report = InlineContractVerifier.Verify(MidpointSource(), "Buggy");
        Assert.Empty(report.Errors);
        Assert.Contains(report.Checks, check =>
            check.ContractId == "MID-LOW" && check.Status == VerificationStatus.Counterexample);
        Assert.False(report.AllProved);
    }

    [Fact]
    public void CorrectMidpointIsProvedFromAnnotatedCSharp()
    {
        var report = InlineContractVerifier.Verify(MidpointSource(), "Correct");
        Assert.Empty(report.Errors);
        Assert.Equal(2, report.Checks.Count);
        Assert.True(report.AllProved);
        Assert.All(report.Checks, c => Assert.Equal(VerificationStatus.Proved, c.Status));
    }

    [Fact]
    public void MissingAttributeIsRejected()
    {
        const string source = """
            using SharpContracts;
            class C {
                public static uint Unmarked(uint x) {
                    Contract.Ensure(x >= 0u, "P", "always");
                    return x;
                }
            }
            """;
        var result = InlineContractVerifier.Verify(source, "Unmarked");
        Assert.NotEmpty(result.Errors);
        Assert.Empty(result.Checks);
    }

    [Fact]
    public void UnmodeledControlFlowFailsClosed()
    {
        const string source = """
            using SharpContracts;
            class C {
                [Verify] public static uint M(uint x) {
                    if (x == 0u) return 1u;
                    Contract.Ensure(x >= 0u, "P", "always");
                    return x;
                }
            }
            """;
        var result=InlineContractVerifier.Verify(source,"M");
        Assert.NotEmpty(result.Errors);
        Assert.Empty(result.Checks);
    }
}
