using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SharpContracts.Solver;
using Xunit;

namespace SharpContracts.Verification.Tests;

/// <summary>One named requirement/semantic case per development iteration; actual CLR/Z3 differential checks.</summary>
public sealed class RequirementIterationTests
{
    public sealed record Case(int Iteration, string Name, string Source, bool Inline, ProofOutcome Expected);
    public static IEnumerable<object[]> Cases() => JsonSerializer.Deserialize<Case[]>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "fixtures", "semantic100.json")),
        new JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } })!
        .Select(c => new object[] { c.Iteration, c.Name, c.Source, c.Inline, c.Expected });

    [Fact]
    public void CorpusHasOneHundredDistinctSequentialRequirements()
    {
        var cases = Cases().ToArray();
        Assert.Equal(100, cases.Length);
        Assert.Equal(Enumerable.Range(1, 100), cases.Select(c => (int)c[0]));
        Assert.Equal(100, cases.Select(c => (string)c[1]).Distinct().Count());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnreachableContractIsUnknownAndEarlierCounterexampleSurvives(bool inline)
    {
        var source = "using SharpContracts; public static class Cases { " +
            (inline ? "[Verify] public static uint M(uint x) { " : "public static void M(uint x) { ") +
            "Contract.Ensure(false,\"P\",\"failure\"); Contract.Ensure(true,\"Q\",\"unreachable\");" +
            (inline ? "return x;" : "") + " } }";
        var result = inline ? InlineContractVerifier.Verify(source, "M") : BoundNumericVerifier.Verify(source, "M");
        Assert.Empty(result.Errors);
        Assert.Equal(VerificationStatus.Counterexample, result.Checks[0].Status);
        Assert.Equal(VerificationStatus.Unknown, result.Checks[1].Status);
        Assert.Contains("Unreachable", result.Checks[1].Reason);
        Assert.Equal(ProofOutcome.Counterexample, ProofReport.From(result).Outcome);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void SemanticRequirement(int iteration, string name, string source, bool inline, ProofOutcome expected)
    {
        Assert.InRange(iteration, 1, 100);
        Assert.False(string.IsNullOrWhiteSpace(name));
        var result = inline ? InlineContractVerifier.Verify(source, "M") : BoundNumericVerifier.Verify(source, "M");
        var report = ProofReport.From(result);
        Assert.True(expected == report.Outcome,
            $"Iteration {iteration}: {name}; expected {expected}, actual {report.Outcome}: {string.Join("; ", report.Diagnostics)}");
        if (expected is ProofOutcome.Unsupported or ProofOutcome.InconsistentAssumptions)
            Assert.Empty(report.Checks);
        if (expected is not (ProofOutcome.Counterexample or ProofOutcome.Proved)) return;
        Assert.Empty(report.Diagnostics);
        Assert.All(report.Checks, c => Assert.True(c.Line > 0));
        Replay(source, report);
    }

    private static void Replay(string source, ProofReport report)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(Contract).Assembly.Location).Distinct()
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("ClrReplay" + Guid.NewGuid().ToString("N"),
            new[] { CSharpSyntaxTree.ParseText(source) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, checkOverflow: false));
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        Assert.True(emitted.Success, string.Join("; ", emitted.Diagnostics));
        stream.Position = 0;
        var context = new AssemblyLoadContext("SharpContracts.Differential", isCollectible: true);
        try
        {
            var assembly = context.LoadFromStream(stream);
            var method = assembly.GetType("Cases")!.GetMethod("M", BindingFlags.Public | BindingFlags.Static)!;
            if (report.Outcome == ProofOutcome.Counterexample)
            {
                var check = Assert.Single(report.Checks, c => c.Status == VerificationStatus.Counterexample);
                Assert.NotNull(check.CounterexampleInputs);
                var inputs = method.GetParameters().Select(p => p.ParameterType == typeof(char)
                    ? (object)(char)ushort.Parse(check.CounterexampleInputs[p.Name!], CultureInfo.InvariantCulture)
                    : Convert.ChangeType(check.CounterexampleInputs[p.Name!], p.ParameterType, CultureInfo.InvariantCulture)).ToArray();
                var thrown = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, inputs));
                var violation = Assert.IsType<ContractViolationException>(thrown.InnerException);
                Assert.Equal(check.ContractId, violation.ContractId);
            }
            else
            {
                // Boundary samples supplement the all-input proof; they are not a replacement for Z3.
                int accepted = 0;
                for (int boundary = 0; boundary < 4; boundary++)
                {
                    var inputs = method.GetParameters().Select(p => Boundary(p.ParameterType, boundary)).ToArray();
                    try { method.Invoke(null, inputs); accepted++; }
                    catch (TargetInvocationException ex) when
                        (ex.InnerException is ContractViolationException { Kind: ContractKind.Precondition }) { }
                }
                Assert.True(accepted > 0, "Boundary replay must exercise a permitted input");
            }
        }
        finally { context.Unload(); }
    }

    private static object Boundary(Type type, int boundary)
    {
        object value = boundary switch
        {
            0 => 0,
            1 => 1,
            2 => type.GetField("MaxValue")!.GetRawConstantValue()!,
            _ => type.GetField("MinValue")!.GetRawConstantValue()!
        };
        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }
}
