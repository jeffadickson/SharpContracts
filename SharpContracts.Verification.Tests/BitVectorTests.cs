using SharpContracts.Solver;
using SharpContracts.Verification;
using Xunit;

namespace SharpContracts.Verification.Tests;

public sealed class BitVectorTests
{
    [Fact]
    public void UncheckedOverflowProducesRealCounterexample()
    {
        const string src = """
        class C {
          void Increment(long x) {
            long y = x + 1;
            Contract.Ensure(y > x, "POST", "strictly increased");
          }
        }
        """;
        var report=BitVectorMethodVerifier.Verify(src,"Increment");
        Assert.Empty(report.Errors);
        Assert.Equal(VerificationStatus.Counterexample,Assert.Single(report.Checks).Status);
    }

    [Fact]
    public void PreconditionProvesUncheckedIncrementSafe()
    {
        const string src = """
        class C {
          void Increment(long x) {
            Contract.Require(x < 9223372036854775807L, "PRE", "not maximum");
            long y = x + 1;
            Contract.Ensure(y > x, "POST", "strictly increased");
          }
        }
        """;
        var report=BitVectorMethodVerifier.Verify(src,"Increment");
        Assert.Empty(report.Errors);
        Assert.True(report.AllProved);
    }

    [Fact]
    public void CheckedIncrementWithoutBoundCanOverflow()
    {
        const string src = """
        class C {
          void Increment(long x) {
            long y = checked(x + 1);
            Contract.Ensure(y > x, "POST", "strictly increased");
          }
        }
        """;
        var report=BitVectorMethodVerifier.Verify(src,"Increment");
        Assert.Empty(report.Errors);
        Assert.Contains(report.Checks, c=>c.ContractId=="CHECKED-OVERFLOW" &&
            c.Status==VerificationStatus.Counterexample);
        Assert.False(report.AllProved);
    }

    [Fact]
    public void CheckedIncrementWithBoundProves()
    {
        const string src = """
        class C {
          void Increment(long x) {
            Contract.Require(x < 9223372036854775807L, "PRE", "not maximum");
            long y = checked(x + 1);
            Contract.Ensure(y > x, "POST", "strictly increased");
          }
        }
        """;
        var report=BitVectorMethodVerifier.Verify(src,"Increment");
        Assert.Empty(report.Errors);
        Assert.True(report.AllProved);
    }

    [Fact]
    public void UnsupportedBranchCannotBeDeclaredProved()
    {
        const string src = """
        class C {
          void Increment(long x) {
            if (x > 0) { x = x + 1; }
            Contract.Ensure(x >= 0, "POST", "safe");
          }
        }
        """;
        var report=BitVectorMethodVerifier.Verify(src,"Increment");
        Assert.NotEmpty(report.Errors);
        Assert.Empty(report.Checks);
    }
    [Fact]
    public void MergedBranchesProveClampedNonnegative()
    {
        const string src = """
        class C {
          void Clamp(long x) {
            long result = 0;
            if (x >= 0) { result = x; }
            else { result = 0; }
            Contract.Ensure(result >= 0, "POST", "clamped");
          }
        }
        """;
        var report=BitVectorMethodVerifier.Verify(src,"Clamp");
        Assert.Empty(report.Errors);
        Assert.True(report.AllProved);
    }

    [Fact]
    public void MergedElseBugReturnsCounterexample()
    {
        const string src = """
        class C {
          void Clamp(long x) {
            long result = 0;
            if (x >= 0) { result = x; }
            else { result = x; }
            Contract.Ensure(result >= 0, "POST", "clamped");
          }
        }
        """;
        var report=BitVectorMethodVerifier.Verify(src,"Clamp");
        Assert.Empty(report.Errors);
        Assert.Equal(VerificationStatus.Counterexample, Assert.Single(report.Checks).Status);
    }

    [Fact]
    public void DifferentBranchTargetsAreRejected()
    {
        const string src = """
        class C {
          void Bad(long x) {
            long a = 0;
            long b = 0;
            if (x >= 0) { a = x; } else { b = x; }
            Contract.Ensure(a >= 0, "POST", "a");
          }
        }
        """;
        var report=BitVectorMethodVerifier.Verify(src,"Bad");
        Assert.NotEmpty(report.Errors);
        Assert.Empty(report.Checks);
    }

    [Fact]
    public void CheckedBranchArithmeticIsRejected()
    {
        const string src = """
        class C {
          void Bad(long x) {
            long y = 0;
            if (x >= 0) { y = checked(x + 1); } else { y = 0; }
            Contract.Ensure(y >= 0, "POST", "safe");
          }
        }
        """;
        var report=BitVectorMethodVerifier.Verify(src,"Bad");
        Assert.NotEmpty(report.Errors);
        Assert.Empty(report.Checks);
    }

}
