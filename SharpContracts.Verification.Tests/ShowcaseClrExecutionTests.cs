using SharpContracts;
using SharpContracts.Examples;
using Xunit;

namespace SharpContracts.Verification.Tests;

/// <summary>Execute the exact annotated C# implementations, not reimplementations of their arithmetic.</summary>
public sealed class ShowcaseClrExecutionTests
{
    [Fact]
    public void MidpointCorrectReturnsExpectedValue()
    {
        Assert.Equal(3_500_000_000u, BinarySearchMidpoint.Correct(3_000_000_000u, 4_000_000_000u));
    }

    [Fact]
    public void MidpointBuggyFailsItsInlinePostcondition()
    {
        var error = Assert.Throws<ContractViolationException>(() =>
            BinarySearchMidpoint.Buggy(3_000_000_000u, 4_000_000_000u));
        Assert.Equal("MID-LOW", error.ContractId);
    }

    [Fact]
    public void PaymentSplitCorrectPreservesOddCents()
    {
        Assert.Equal(50u, PaymentSplit.Correct(101u));
    }

    [Fact]
    public void PaymentSplitBuggyDetectsLostCent()
    {
        var error = Assert.Throws<ContractViolationException>(() => PaymentSplit.Buggy(101u));
        Assert.Equal("SPLIT-CONSERVE", error.ContractId);
    }

    [Fact]
    public void BufferAdmissionBuggyDetectsWrappedRequest()
    {
        var error = Assert.Throws<ContractViolationException>(() =>
            BufferAdmission.Buggy(uint.MaxValue - 2u, 5u, uint.MaxValue - 1u));
        Assert.Equal("BUFFER-FITS", error.ContractId);
    }

    [Fact]
    public void BufferAdmissionCorrectRejectsUnsafeRequest()
    {
        var error = Assert.Throws<ContractViolationException>(() =>
            BufferAdmission.Correct(uint.MaxValue - 2u, 5u, uint.MaxValue - 1u));
        Assert.Equal("BUFFER-ADMIT", error.ContractId);
    }

    [Fact]
    public void BufferAdmissionCorrectAcceptsSafeRequest()
    {
        Assert.Equal(35u, BufferAdmission.Correct(10u, 25u, 50u));
    }
}
