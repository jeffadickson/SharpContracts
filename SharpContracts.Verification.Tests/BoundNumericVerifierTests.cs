using SharpContracts.Solver;
using SharpContracts.Verification;
using Xunit;

namespace SharpContracts.Verification.Tests;

public sealed class BoundNumericVerifierTests
{
    [Fact]
    public void ByteOperandsPromoteToInt32AndConservationProves()
    {
        const string source = """
        class C {
            void Check(byte a, byte b) {
                SharpContracts.Contract.Ensure(a + b >= a, "BYTE", "promoted addition");
            }
        }
        """;
        var report=BoundNumericVerifier.Verify(source,"Check");
        Assert.Empty(report.Errors);
        Assert.True(report.AllProved);
    }

    [Fact]
    public void SignedInt32OverflowIsCounterexample()
    {
        const string source = """
        class C {
            void Check(int x) {
                SharpContracts.Contract.Ensure(x + 1 > x, "INT", "increases");
            }
        }
        """;
        var report=BoundNumericVerifier.Verify(source,"Check");
        Assert.Empty(report.Errors);
        Assert.Equal(VerificationStatus.Counterexample,Assert.Single(report.Checks).Status);
    }

    [Fact]
    public void Unsigned64BitWraparoundIsCounterexample()
    {
        const string source = """
        class C {
            void Check(ulong x) {
                SharpContracts.Contract.Ensure(x + 1UL > x, "ULONG", "increases");
            }
        }
        """;
        var report=BoundNumericVerifier.Verify(source,"Check");
        Assert.Empty(report.Errors);
        Assert.Equal(VerificationStatus.Counterexample,Assert.Single(report.Checks).Status);
    }

    [Fact]
    public void CheckedArithmeticFailsClosed()
    {
        const string source = """
        class C {
            void Check(int x) {
                SharpContracts.Contract.Ensure(checked(x + 1) > x, "CHECKED", "increases");
            }
        }
        """;
        var report=BoundNumericVerifier.Verify(source,"Check");
        Assert.NotEmpty(report.Errors);
        Assert.Empty(report.Checks);
    }
    [Fact]
    public void ULongMaxLiteralDoesNotOverflowHostConversion()
    {
        const string source = """
        class C {
            void Check(ulong x) {
                SharpContracts.Contract.Ensure(x <= 18446744073709551615UL,
                    "ULONG-MAX", "All UInt64 values are at most UInt64.MaxValue");
            }
        }
        """;
        var report=BoundNumericVerifier.Verify(source,"Check");
        Assert.Empty(report.Errors);
        Assert.True(report.AllProved);
    }

    [Fact]
    public void ULongMaxLiteralCanProvideCounterexample()
    {
        const string source = """
        class C {
            void Check(ulong x) {
                SharpContracts.Contract.Ensure(x < 18446744073709551615UL,
                    "ULONG-STRICT", "All values are below UInt64.MaxValue");
            }
        }
        """;
        var report=BoundNumericVerifier.Verify(source,"Check");
        Assert.Empty(report.Errors);
        Assert.Equal(VerificationStatus.Counterexample,Assert.Single(report.Checks).Status);
    }

    [Fact]
    public void DecimalContractCannotBeMistakenForBitVector()
    {
        const string source = """
        class C {
            void Check(decimal x) {
                SharpContracts.Contract.Ensure(x + 1m > x, "DECIMAL", "monotonic");
            }
        }
        """;
        var report=BoundNumericVerifier.Verify(source,"Check");
        Assert.NotEmpty(report.Errors);
        Assert.False(report.AllProved);
        Assert.Empty(report.Checks);
    }

    [Fact]
    public void CustomNumericOperatorCannotBeAssumedBuiltIn()
    {
        const string source = """
        struct Voltage {
            public int Millivolts;
            public static Voltage operator +(Voltage x, Voltage y) => x;
            public static bool operator >(Voltage x, Voltage y) => true;
            public static bool operator <(Voltage x, Voltage y) => false;
        }
        class C {
            void Check(Voltage x, Voltage y) {
                SharpContracts.Contract.Ensure(x + y > x, "CUSTOM", "operator contract");
            }
        }
        """;
        var report=BoundNumericVerifier.Verify(source,"Check");
        Assert.NotEmpty(report.Errors);
        Assert.Empty(report.Checks);
        Assert.False(report.AllProved);
    }

    [Fact]
    public void ContradictoryPreconditionsAreNotPresentedAsProof()
    {
        const string source = """
        class C {
            void Check(int x) {
                SharpContracts.Contract.Require(x > 5, "PRE-A", "large");
                SharpContracts.Contract.Require(x < 0, "PRE-B", "negative");
                SharpContracts.Contract.Ensure(x == 123, "POST", "arbitrary");
            }
        }
        """;
        var report=BoundNumericVerifier.Verify(source,"Check");
        Assert.NotEmpty(report.Errors);
        Assert.Contains(report.Errors,e=>e.Contains("Inconsistent preconditions"));
        Assert.False(report.AllProved);
        Assert.Empty(report.Checks);
    }

    [Fact]
    public void FeasiblePreconditionsCanProvePostcondition()
    {
        const string source = """
        class C {
            void Check(int x) {
                SharpContracts.Contract.Require(x > 5, "PRE", "large");
                SharpContracts.Contract.Ensure(x > 0, "POST", "positive");
            }
        }
        """;
        var report=BoundNumericVerifier.Verify(source,"Check");
        Assert.Empty(report.Errors);
        Assert.True(report.AllProved);
    }

}
