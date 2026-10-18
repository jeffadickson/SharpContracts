using System;
using System.IO;
using SharpContracts.Solver;
using SharpContracts.Verification;
using Xunit;

namespace SharpContracts.Verification.Tests;

/// <summary>Source-derived proof checks for the sales demonstration examples.</summary>
public sealed class SalesShowcaseTests
{
    private static string Source(string name) => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "fixtures", name));

    [Theory]
    [InlineData("PaymentSplit.cs", "Buggy", VerificationStatus.Counterexample)]
    [InlineData("PaymentSplit.cs", "Correct", VerificationStatus.Proved)]
    [InlineData("BufferAdmission.cs", "Buggy", VerificationStatus.Counterexample)]
    [InlineData("BufferAdmission.cs", "Correct", VerificationStatus.Proved)]
    public void VerifyAnnotatedSource(string file, string method, VerificationStatus expected)
    {
        var report = InlineContractVerifier.Verify(Source(file), method);
        Assert.Empty(report.Errors);
        Assert.NotEmpty(report.Checks);
        if (expected == VerificationStatus.Proved) Assert.True(report.AllProved);
        else Assert.Contains(report.Checks, c => c.Status == expected);
    }
}