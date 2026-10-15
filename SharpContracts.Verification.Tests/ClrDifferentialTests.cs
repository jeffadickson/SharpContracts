using System;
using SharpContracts.Solver;
using SharpContracts.Verification;
using Xunit;

namespace SharpContracts.Verification.Tests;

/// <summary>
/// Differential checks: compare the verifier's SAT/UNSAT conclusions against compiled
/// C# arithmetic at boundary inputs. These are regression samples, not a soundness proof.
/// </summary>
public sealed class ClrDifferentialTests
{
    private const string IncrementInt = """
        using SharpContracts;
        class C {
            void Check(int x) {
                Contract.Ensure(unchecked(x + 1) > x, "I32", "increases");
            }
        }
        """;

    private const string IncrementUInt64 = """
        using SharpContracts;
        class C {
            void Check(ulong x) {
                Contract.Ensure(unchecked(x + 1UL) > x, "U64", "increases");
            }
        }
        """;

    private const string PromotedBytes = """
        using SharpContracts;
        class C {
            void Check(byte a, byte b) {
                Contract.Ensure(a + b >= a, "PROMOTION", "monotonic");
            }
        }
        """;

    [Fact]
    public void SignedInt32CounterexampleMatchesCompiledCSharp()
    {
        var proof = BoundNumericVerifier.Verify(IncrementInt, "Check");
        Assert.Empty(proof.Errors);
        Assert.Equal(VerificationStatus.Counterexample, Assert.Single(proof.Checks).Status);

        // Exact CLR arithmetic at overflow boundary.
        int x = int.MaxValue;
        int y = unchecked(x + 1);
        Assert.Equal(int.MinValue, y);
        Assert.False(y > x);
        foreach (int candidate in new[] { int.MinValue, -1, 0, 1, int.MaxValue - 1 })
            Assert.True(unchecked(candidate + 1) > candidate);
    }

    [Fact]
    public void UnsignedInt64CounterexampleMatchesCompiledCSharp()
    {
        var proof = BoundNumericVerifier.Verify(IncrementUInt64, "Check");
        Assert.Empty(proof.Errors);
        Assert.Equal(VerificationStatus.Counterexample, Assert.Single(proof.Checks).Status);
        ulong x = ulong.MaxValue;
        ulong y = unchecked(x + 1UL);
        Assert.Equal(0UL, y);
        Assert.False(y > x);
    }

    [Fact]
    public void BytePromotionProofMatchesAllUnsignedEightBitInputs()
    {
        var proof = BoundNumericVerifier.Verify(PromotedBytes, "Check");
        Assert.Empty(proof.Errors);
        Assert.True(proof.AllProved);
        for (int a = byte.MinValue; a <= byte.MaxValue; a++)
        for (int b = byte.MinValue; b <= byte.MaxValue; b++)
        {
            byte aa = (byte)a, bb = (byte)b;
            int sum = aa + bb; // C# promotes byte + byte to signed int.
            Assert.True(sum >= aa);
        }
    }

    [Fact]
    public void CheckedClrOverflowIsAnExceptionNotAWrappedValue()
    {
        int max = int.MaxValue;
        Assert.Throws<OverflowException>(() => checked(max + 1));
        // The compiler-bound proof engine must not claim this expression is modeled.
        var source = """
            using SharpContracts;
            class C {
                void Check(int x) {
                    Contract.Ensure(checked(x + 1) > x, "CHECKED", "increases");
                }
            }
            """;
        var proof = BoundNumericVerifier.Verify(source, "Check");
        Assert.NotEmpty(proof.Errors);
        Assert.False(proof.AllProved);
    }
}
