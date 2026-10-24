using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace SharpContracts.Verification.Tests;

/// <summary>SC-090: named source-derived shift/assignment iterations, with exact compiled CLR replay.</summary>
public sealed class ShiftRequirementTests(ITestOutputHelper output)
{
    /// <summary>Stable case IDs also serve as traceability anchors in the StrictDoc evidence ledger.</summary>
    public sealed record Case(int Iteration,string Name,string Source,bool Inline,ProofOutcome Expected);

    public static IEnumerable<object[]> Cases() => JsonSerializer.Deserialize<Case[]>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory,"fixtures","shifts50.json")),
        new JsonSerializerOptions { Converters={new JsonStringEnumConverter()} })!
        .Select(c=>new object[]{c.Iteration,c.Name,c.Source,c.Inline,c.Expected});

    [Fact]
    public void FiftyDistinctSequentialIterationsAreRetained()
    {
        var cases=Cases().ToArray();
        Assert.Equal(50,cases.Length);
        Assert.Equal(Enumerable.Range(1,50),cases.Select(c=>(int)c[0]));
        Assert.Equal(50,cases.Select(c=>(string)c[1]).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void SourceShiftMatchesClr(int iteration,string name,string source,bool inline,ProofOutcome expected)
    {
        Assert.InRange(iteration,1,50);
        var result=inline ? InlineContractVerifier.Verify(source,"M") : BoundNumericVerifier.Verify(source,"M");
        var report=ProofReport.From(result);
        // Passing counterexample tests still retain the actual solver input model in their TRX output.
        output.WriteLine(report.ToJson());
        Assert.True(report.Outcome==expected,$"Iteration {iteration}: {name}: expected {expected}, actual {report.Outcome}: {string.Join("; ",report.Diagnostics)}");
        if (expected==ProofOutcome.Unsupported) { Assert.Empty(report.Checks); return; }
        Assert.Empty(report.Diagnostics);
        RequirementIterationTests.Replay(source,report);
    }
}
