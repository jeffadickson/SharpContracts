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

    [Fact] public void FailsClosedOnUnsupportedControlFlow()
    {
        const string source = """
            class C {
              void M(long balance) {
                if (balance > 0) { balance = balance - 1; }
                Contract.Ensure(balance >= 0, "POST", "valid");
              }
            }
            """;
        var result=MethodVerifier.Verify(source,"M");
        Assert.NotEmpty(result.Errors);
        Assert.Empty(result.Checks);
    }
}
