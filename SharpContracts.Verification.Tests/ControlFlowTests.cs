using System.Text.Json;
using System.Text.Json.Serialization;
using FsCheck.Xunit;
using SharpContracts.Solver;
using Xunit;
using Xunit.Abstractions;

namespace SharpContracts.Verification.Tests;

/// <summary>SC-091/092: source-to-Z3 paths, source-linked CLR replay and fail-closed resource limits.</summary>
public sealed class ControlFlowTests(ITestOutputHelper output)
{
    public sealed record Case(string Name,string Source,ProofOutcome Expected);
    public static IEnumerable<object[]> Cases()=>JsonSerializer.Deserialize<Case[]>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory,"fixtures","control-flow.json")),
        new JsonSerializerOptions{Converters={new JsonStringEnumConverter()}})!
        .Select(c=>new object[]{c.Name,c.Source,c.Expected});

    [Theory]
    [MemberData(nameof(Cases))]
    public void StructuredSourceMatchesExpectedOutcome(string name,string source,ProofOutcome expected)
    {
        var report=ProofReport.From(InlineContractVerifier.Verify(source,"M"));
        output.WriteLine(report.ToJson());
        Assert.True(report.Outcome==expected,$"{name}: expected {expected}, actual {report.Outcome}: {string.Join("; ",report.Diagnostics)}");
        if (expected is ProofOutcome.Unsupported or ProofOutcome.InconsistentAssumptions) Assert.Empty(report.Checks);
        if (expected is ProofOutcome.Proved or ProofOutcome.Counterexample) RequirementIterationTests.Replay(source,report);
        if (expected==ProofOutcome.Unknown) Assert.Contains(report.Checks,c=>c.Status==VerificationStatus.Unknown && c.Reason!.Contains("Unreachable"));
    }

    [Fact]
    public void SameLabelOnDistinctSourceSitesIsNotMerged()
    {
        var result=InlineContractVerifier.Verify(LocalAssignmentTests.Source(
            "if(x==0u) { Contract.Assert(true,\"P\",\"zero\"); } else { Contract.Assert(true,\"P\",\"nonzero\"); } return x;"),"M");
        Assert.True(result.AllProved);
        Assert.Equal(2,result.Checks.Count);
    }

    [Theory]
    [InlineData("paths")]
    [InlineData("nesting")]
    [InlineData("expression-size")]
    [InlineData("expression-depth")]
    [InlineData("source-expression-depth")]
    [InlineData("statements")]
    public void ExceededBudgetsDiscardEveryPartialProof(string kind)
    {
        string body=kind switch
        {
            "paths"=>"uint y=x; Contract.Assert(true,\"A\",\"prefix\"); "+string.Join(" ",Enumerable.Range(0,8).Select(i=>
                $"if((x & (1u << {i}))!=0u) y=1u; else y=0u;"))+" Contract.Ensure(y<=1u,\"P\",\"range\"); return y;",
            "nesting"=>new string('{',70)+"Contract.Ensure(true,\"P\",\"nested\"); return x;"+new string('}',70),
            "expression-size"=>"uint y=x; "+string.Concat(Enumerable.Repeat("y=y+y;",14))+"Contract.Ensure(true,\"P\",\"tautology\"); return y;",
            "expression-depth"=>"uint y=x; "+string.Concat(Enumerable.Repeat("y=y+1u;",130))+"Contract.Ensure(true,\"P\",\"tautology\"); return y;",
            "source-expression-depth"=>"uint y=x"+string.Concat(Enumerable.Repeat("+1u",200))+"; Contract.Ensure(true,\"P\",\"tautology\"); return y;",
            _=>"uint y=x; "+string.Concat(Enumerable.Repeat("y=x;",1025))+"Contract.Ensure(true,\"P\",\"tautology\"); return y;"
        };
        var result=InlineContractVerifier.Verify(LocalAssignmentTests.Source(body),"M");
        Assert.Empty(result.Checks);
        Assert.Contains(result.Errors,e=>e.Contains("budget"));
    }
}

/// <summary>Small targeted proof/replay cases keep deliberate control-flow mutations fast and reproducible.</summary>
public sealed class ControlFlowSanityTests
{
    [Theory]
    [InlineData("uint y=x; if(x==0u)y=1u; else y=x; Contract.Ensure(y==x,\"P\",\"defect in true arm\"); return y;",ProofOutcome.Counterexample)]
    [InlineData("uint y=x; if(x==0u)y=0u; else y=unchecked(x+1u); Contract.Ensure(y==x,\"P\",\"defect in false arm\"); return y;",ProofOutcome.Counterexample)]
    [InlineData("if(x==0u)return 1u; Contract.Ensure(x>0u,\"P\",\"after early return\"); return x;",ProofOutcome.Proved)]
    [InlineData("uint y=x; if(x==0u)y=1u; else y=x; Contract.Ensure(y>=x,\"P\",\"separate states\"); return y;",ProofOutcome.Proved)]
    [InlineData("if(x==x) { Contract.Assert(true,\"P\",\"live\"); } else { uint y=checked(x+1u); } return x;",ProofOutcome.Unsupported)]
    public void BranchSemanticsAreObservableInCompiledCSharp(string body,ProofOutcome expected)
    {
        var source=LocalAssignmentTests.Source(body);
        var report=ProofReport.From(InlineContractVerifier.Verify(source,"M"));
        Assert.Equal(expected,report.Outcome);
        if (expected==ProofOutcome.Unsupported) Assert.Empty(report.Checks);
        else RequirementIterationTests.Replay(source,report);
    }
}

/// <summary>Fixed-seed thresholds vary the source while each solver query quantifies over all UInt32 inputs.</summary>
public sealed class ControlFlowPropertyTests
{
    [Property(MaxTest=32,Replay="20261024,24681357")]
    public bool ThresholdBranchProvesAndReplays(uint threshold)
    {
        var literal=threshold.ToString(System.Globalization.CultureInfo.InvariantCulture)+"u";
        var source=LocalAssignmentTests.Source($"uint y=x; if(x<{literal})y={literal}; else y=x; Contract.Ensure(y>=x,\"P\",\"threshold\"); return y;");
        var result=InlineContractVerifier.Verify(source,"M");
        if (!result.AllProved) return false;
        RequirementIterationTests.Replay(source,ProofReport.From(result));
        return true;
    }
}
