using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Z3;
using SharpContracts.Solver;
using SharpContracts.Symbolic;

namespace SharpContracts.Verification;

/// <summary>
/// Compiler-bound proof for a deliberately restricted parameter-only numeric contract subset.
/// Unlike the legacy BV64 engine, Roslyn IOperation supplies promotions and result types.
/// Unsupported constructs abort the entire report. All arithmetic is currently unchecked.
/// </summary>
public static class BoundNumericVerifier
{
    public static MethodVerification Verify(string source, string methodName)
    {
        var errors = new List<string>();
        var results = new List<MethodCheck>();
        var tree = CSharpSyntaxTree.ParseText(source);
        var platform = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        if (string.IsNullOrEmpty(platform))
            return new(Array.Empty<MethodCheck>(),new[]{"Platform metadata unavailable"});
        var refs = platform.Split(System.IO.Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p)).Cast<MetadataReference>().ToList();
        refs.Add(MetadataReference.CreateFromFile(typeof(SharpContracts.Contract).Assembly.Location));
        var compilation = CSharpCompilation.Create("SharpContracts.BoundProof",
            new[]{tree},refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        errors.AddRange(compilation.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).Select(d=>d.ToString()));
        if(errors.Count>0) return new(Array.Empty<MethodCheck>(),errors);
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m=>m.Identifier.ValueText==methodName).ToArray();
        if(method.Length!=1 || method[0].Body is null || method[0].ReturnType.ToString()!="void" ||
           method[0].Modifiers.Any(m=>m.IsKind(SyntaxKind.AsyncKeyword)))
            return new(Array.Empty<MethodCheck>(),new[]{"Requires a unique synchronous void method with a block body"});
        var semantic=compilation.GetSemanticModel(tree);
        try
        {
            using var ctx=new Context();
            using var solver=ctx.MkSolver();
            using var options=ctx.MkParams();
            options.Add("timeout",5000u);
            solver.Parameters=options;
            var parameters = new Dictionary<ISymbol,ClrExpression>(SymbolEqualityComparer.Default);
            var lowerer = new ClrOperationLowerer(parameters);
            var encoder = new ClrExpressionEncoder(ctx);
            foreach(var p in method[0].ParameterList.Parameters)
            {
                if(semantic.GetDeclaredSymbol(p) is not IParameterSymbol symbol ||
                   symbol.RefKind!=RefKind.None || p.Default is not null)
                    throw new NotSupportedException("Only ordinary parameters supported");
                var kind=NumericTypeDescriptor.FromSymbol(symbol.Type);
                if(kind.Kind is not (NumericModelKind.SignedBitVector or NumericModelKind.UnsignedBitVector) ||
                    kind.Width is not (32 or 64 or 16 or 8))
                    throw new NotSupportedException("Unsupported parameter type: "+symbol.Type);
                parameters.Add(symbol,new ClrParameter(symbol.Name, ClrOperationLowerer.ScalarType(symbol.Type)));
            }
            bool hasEnsure=false;
            foreach(var statement in method[0].Body!.Statements)
            {
                if(statement is not ExpressionStatementSyntax {Expression: InvocationExpressionSyntax call} ||
                   semantic.GetOperation(call) is not IInvocationOperation invocation ||
                   !RuntimeSymbolPolicy.IsRuntimeType(invocation.TargetMethod.ContainingType,"SharpContracts.Contract") ||
                   invocation.TargetMethod.Name is not ("Require" or "Ensure"))
                    throw new NotSupportedException("Only Contract.Require/Ensure statements are supported");
                var inputs=invocation.Arguments.ToDictionary(x=>x.Parameter!.Name,x=>x.Value);
                if(!inputs.TryGetValue("condition",out var condition) ||
                   !inputs.TryGetValue("id",out var id) || !id.ConstantValue.HasValue ||
                   id.ConstantValue.Value is not string label || string.IsNullOrWhiteSpace(label) ||
                   !inputs.TryGetValue("message",out var message) ||
                   !message.ConstantValue.HasValue || message.ConstantValue.Value is not string)
                    throw new NotSupportedException("Contract requires nonblank constant ID and constant message");
                // The source verifier is pure and parameter-only. Reject any annotation expression
                // that could execute code, throw, or mutate state outside the modeled predicate.
                if(hasEnsure && invocation.TargetMethod.Name=="Require")
                    throw new NotSupportedException("Require after Ensure unsupported");
                var lowered=encoder.Encode(lowerer.Lower(condition));
                if(lowered is not BoolExpr predicate) throw new NotSupportedException("Expected Boolean condition");
                if(invocation.TargetMethod.Name=="Require") solver.Assert(predicate);
                else
                {
                    results.Add(ProofQuery.Check(ctx,solver,predicate,label,
                        tree.GetLineSpan(statement.Span).StartLinePosition.Line+1,
                        hasEnsure,parameters,encoder));
                    hasEnsure=true;
                    solver.Assert(predicate);
                }
            }
        }
        catch(Exception ex) when(ex is NotSupportedException or ArgumentException or Z3Exception or InvalidOperationException)
        {
            errors.Add(ex.Message);
        }
        return errors.Count>0?new(Array.Empty<MethodCheck>(),errors):new(results,errors);
    }

}
