using System;
using System.Linq;
using System.Text.Json;
using SharpContracts.Solver;

namespace SharpContracts.Verification;

/// <summary>Stable verification outcome for automation and machine-readable reports.</summary>
public enum ProofOutcome { Proved, Counterexample, Unknown, InconsistentAssumptions, Unsupported, NoContracts }

/// <summary>Versioned verification report; never interprets an error as proof.</summary>
public sealed record ProofReport(
    int SchemaVersion,
    ProofOutcome Outcome,
    MethodCheck[] Checks,
    string[] Diagnostics)
{
    /// <summary>Converts the legacy verifier result without losing an UNKNOWN check.</summary>
    public static ProofReport From(MethodVerification result)
    {
        var diagnostics = result.Errors.ToArray();
        ProofOutcome outcome;
        if (diagnostics.Any(d => d.StartsWith("Inconsistent preconditions:", StringComparison.Ordinal)))
            outcome = ProofOutcome.InconsistentAssumptions;
        else if (diagnostics.Any(d => d.StartsWith("Precondition feasibility unknown:", StringComparison.Ordinal)))
            outcome = ProofOutcome.Unknown;
        else if (diagnostics.Length != 0)
            outcome = ProofOutcome.Unsupported;
        else if (result.Checks.Count == 0)
            outcome = ProofOutcome.NoContracts;
        else if (result.Checks.Any(c => c.Status == VerificationStatus.Counterexample))
            outcome = ProofOutcome.Counterexample;
        else if (result.Checks.Any(c => c.Status == VerificationStatus.Unknown))
            outcome = ProofOutcome.Unknown;
        else
            outcome = ProofOutcome.Proved;
        return new(1, outcome, result.Checks.ToArray(), diagnostics);
    }

    /// <summary>Success only when all checks are proved and no errors are present.</summary>
    public int ExitCode => Outcome switch
    {
        ProofOutcome.Proved => 0,
        ProofOutcome.Counterexample => 1,
        ProofOutcome.Unknown => 3,
        ProofOutcome.InconsistentAssumptions => 4,
        _ => 2
    };

    /// <summary>Stable JSON with camel-cased fields for CI tooling.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    });
}
