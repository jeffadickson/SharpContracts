using SharpContracts.Verification;
using SharpContracts.Solver;
using Xunit;

namespace SharpContracts.Verification.Tests;

public sealed class ProofReportTests
{
    [Fact] public void InconsistentAssumptionsHaveDistinctStatus()
    {
        var report=ProofReport.From(new MethodVerification(
            System.Array.Empty<MethodCheck>(),
            new[]{"Inconsistent preconditions: no permitted inputs"}));
        Assert.Equal(ProofOutcome.InconsistentAssumptions,report.Outcome);
        Assert.Equal(4,report.ExitCode);
        Assert.Contains("inconsistentAssumptions",report.ToJson(),System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void CounterexamplesNeverExitSuccessfully()
    {
        var report=ProofReport.From(new MethodVerification(
            new[]{new MethodCheck("POST",10,VerificationStatus.Counterexample,"x=4",null)},
            System.Array.Empty<string>()));
        Assert.Equal(ProofOutcome.Counterexample,report.Outcome);
        Assert.Equal(1,report.ExitCode);
        Assert.Contains("schemaVersion",report.ToJson());
    }

    [Fact] public void UnknownAndUnsupportedAreDistinct()
    {
        var unknown=ProofReport.From(new MethodVerification(
            new[]{new MethodCheck("P",1,VerificationStatus.Unknown,null,"timeout")},
            System.Array.Empty<string>()));
        Assert.Equal(3,unknown.ExitCode);
        var unsupported=ProofReport.From(new MethodVerification(
            System.Array.Empty<MethodCheck>(),new[]{"Unsupported expression"}));
        Assert.Equal(2,unsupported.ExitCode);
    }
}