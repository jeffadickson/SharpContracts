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
    public void GuardedEarlyReturnPreservesCallSiteContractSemantics()
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
        Assert.Empty(result.Errors);
        Assert.True(result.AllProved);
    }
    [Fact]
    public void ArbitraryDivisorIsRejectedRatherThanModeledAsTotal()
    {
        const string source = """
            using SharpContracts;
            class C {
                [Verify] static uint M(uint a, uint d) {
                    uint mid = a / d;
                    Contract.Ensure(mid <= a, "P", "must fit");
                    return mid;
                }
            }
            """;
        var report=InlineContractVerifier.Verify(source,"M");
        Assert.NotEmpty(report.Errors);
        Assert.Empty(report.Checks);
    }

    [Fact]
    public void DynamicMessageIsRejectedRatherThanExecutedDuringProof()
    {
        const string source = """
            using SharpContracts;
            class C {
                static string Message() => "dynamic";
                [Verify] static uint M(uint a) {
                    Contract.Ensure(a == a, "P", Message());
                    return a;
                }
            }
            """;
        var report=InlineContractVerifier.Verify(source,"M");
        Assert.NotEmpty(report.Errors);
        Assert.Empty(report.Checks);
    }

    [Fact]
    public void CheckedArithmeticIsRejected()
    {
        const string source = """
            using SharpContracts;
            class C {
                [Verify] static uint M(uint a) {
                    uint mid = checked(a + 1u);
                    Contract.Ensure(mid >= a, "P", "increases");
                    return mid;
                }
            }
            """;
        var report=InlineContractVerifier.Verify(source,"M");
        Assert.NotEmpty(report.Errors);
        Assert.Empty(report.Checks);
    }

}
