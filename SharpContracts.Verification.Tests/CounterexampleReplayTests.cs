using System.Globalization;
using SharpContracts.Examples;
using SharpContracts.Solver;
using Xunit;

namespace SharpContracts.Verification.Tests;

public sealed class CounterexampleReplayTests
{
    [Theory]
    [InlineData("BinarySearchMidpoint.cs", "MID-LOW")]
    [InlineData("PaymentSplit.cs", "SPLIT-CONSERVE")]
    [InlineData("BufferAdmission.cs", "BUFFER-FITS")]
    public void SolverInputsReplayAgainstExactCompiledSource(string file, string contract)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", file));
        var result = InlineContractVerifier.Verify(source, "Buggy");
        Assert.Empty(result.Errors);
        var check = Assert.Single(result.Checks, c => c.Status == VerificationStatus.Counterexample);
        Assert.Equal(contract, check.ContractId);
        Assert.True(check.Line > 0);
        Assert.NotNull(check.CounterexampleInputs);
        uint Input(string name) => uint.Parse(check.CounterexampleInputs[name], CultureInfo.InvariantCulture);
        var failure = Assert.Throws<ContractViolationException>(() =>
        {
            switch (file)
            {
                case "BinarySearchMidpoint.cs":
                    BinarySearchMidpoint.Buggy(Input("low"), Input("high"));
                    break;
                case "PaymentSplit.cs":
                    PaymentSplit.Buggy(Input("cents"));
                    break;
                case "BufferAdmission.cs":
                    BufferAdmission.Buggy(Input("used"), Input("incoming"), Input("capacity"));
                    break;
                default: throw new InvalidOperationException(file);
            }
        });
        Assert.Equal(contract, failure.ContractId);
        Assert.Contains("counterexampleInputs", ProofReport.From(result).ToJson());
    }

    [Fact]
    public void InconsistentInlinePreconditionsHaveDistinctReportAndExitCode()
    {
        const string source = """
            using SharpContracts;
            class C {
                [Verify] static uint M(uint x) {
                    Contract.Require(x > 5u, "A", "large");
                    Contract.Require(x < 3u, "B", "small");
                    Contract.Ensure(x == 0u, "P", "arbitrary");
                    return x;
                }
            }
            """;
        var report = ProofReport.From(InlineContractVerifier.Verify(source, "M"));
        Assert.Equal(ProofOutcome.InconsistentAssumptions, report.Outcome);
        Assert.Equal(4, report.ExitCode);
        Assert.Empty(report.Checks);
    }

    [Theory]
    [InlineData("ref uint x")]
    [InlineData("in uint x")]
    public void ByReferenceParametersFailClosed(string parameter)
    {
        var source = $$"""
            using SharpContracts;
            class C {
                [Verify] static uint M({{parameter}}) {
                    Contract.Ensure(x == x, "P", "identity");
                    return x;
                }
            }
            """;
        var report = ProofReport.From(InlineContractVerifier.Verify(source, "M"));
        Assert.Equal(ProofOutcome.Unsupported, report.Outcome);
        Assert.Empty(report.Checks);
    }

    [Theory]
    [InlineData("unchecked(x + 1u)", ProofOutcome.Counterexample)]
    [InlineData("checked(x + 1u)", ProofOutcome.Unsupported)]
    public void ReturnWrapperUsesBoundOperationAndPreservesOverflowContext(string expression, ProofOutcome outcome)
    {
        var source = $$"""
            using SharpContracts;
            class C {
                [Verify] static uint M(uint x) {
                    Contract.Ensure(x + 1u > x, "P", "increase");
                    return {{expression}};
                }
            }
            """;
        var report = ProofReport.From(InlineContractVerifier.Verify(source, "M"));
        Assert.Equal(outcome, report.Outcome);
        if (outcome == ProofOutcome.Unsupported) Assert.Empty(report.Checks);
    }
}
