using SharpContracts.Solver;
using SharpContracts.Verification;
using Xunit;

namespace SharpContracts.Verification.Tests;
public sealed class MethodVerifierTests
{
    [Fact] public void ProvesStraightLineMethod()
    {
        const string source = """
            class C {
              void Transfer(long balance, long amount) {
                Contract.Require(balance >= amount, "PRE1", "sufficient");
                Contract.Require(amount >= 0, "PRE2", "nonnegative");
                long remaining = balance - amount;
                Contract.Ensure(remaining >= 0, "POST1", "nonnegative result");
              }
            }
            """;
        var result=MethodVerifier.Verify(source,"Transfer");
        Assert.Empty(result.Errors);
        Assert.Single(result.Checks);
        Assert.Equal(VerificationStatus.Proved,result.Checks[0].Status);
        Assert.Equal("POST1",result.Checks[0].ContractId);
    }

    [Fact] public void ReportsCounterexampleWhenPreconditionMissing()
    {
        const string source = """
            class C {
              void Transfer(long balance, long amount) {
                Contract.Require(amount >= 0, "PRE1", "nonnegative");
                long remaining = balance - amount;
                Contract.Ensure(remaining >= 0, "POST1", "nonnegative result");
              }
            }
            """;
        var result=MethodVerifier.Verify(source,"Transfer");
        Assert.Empty(result.Errors);
        Assert.Equal(VerificationStatus.Counterexample,result.Checks[0].Status);
        Assert.NotNull(result.Checks[0].Counterexample);
    }

    [Fact] public void VerifiesBothBranchesIndependently()
    {
        const string source = """
            class C {
              void Clamp(long x) {
                long result = 0;
                if (x >= 0) { result = x; }
                else { result = 0; }
                Contract.Ensure(result >= 0, "POST", "nonnegative");
              }
            }
            """;
        var report=MethodVerifier.Verify(source,"Clamp");
        Assert.Empty(report.Errors);
        Assert.Equal(2,report.Checks.Count);
        Assert.True(report.AllProved);
    }

    [Fact] public void FindsBugInElseBranch()
    {
        const string source = """
            class C {
              void Clamp(long x) {
                long result = 0;
                if (x >= 0) { result = x; }
                else { result = x; }
                Contract.Ensure(result >= 0, "POST", "nonnegative");
              }
            }
            """;
        var report=MethodVerifier.Verify(source,"Clamp");
        Assert.Empty(report.Errors);
        Assert.Contains(report.Checks,c=>c.Status==VerificationStatus.Counterexample);
        Assert.False(report.AllProved);
    }

    [Fact] public void FailsClosedOnLoopEvenAfterAValidCheck()
    {
        const string source = """
            class C {
              void M(long x) {
                Contract.Ensure(x == x, "POST", "valid");
                while (x > 0) { x = x - 1; }
              }
            }
            """;
        var report=MethodVerifier.Verify(source,"M");
        Assert.NotEmpty(report.Errors);
        Assert.Empty(report.Checks);
        Assert.False(report.AllProved);
    }

    [Fact] public void FailsClosedOnMalformedCSharp()
    {
        var report=MethodVerifier.Verify("class C { void M(long x) { if ( } }","M");
        Assert.NotEmpty(report.Errors);
        Assert.Empty(report.Checks);
    }
}
