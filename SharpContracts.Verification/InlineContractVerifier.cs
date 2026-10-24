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
/// Proof engine for [Verify] methods with uint parameters, locals, inline contracts,
/// with structured branches and returns. Expressions use compiler-resolved integral bit vectors;
/// checked and unsupported operations fail closed.
/// </summary>
public static class InlineContractVerifier
{
    /// <summary>Verify one annotated method in a complete compilable C# source file.</summary>
    public static MethodVerification Verify(string source, string methodName)
    {
        var errors = new List<string>();
        var checks = new List<MethodCheck>();
        var tree = CSharpSyntaxTree.ParseText(source);
        var platform = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        if (string.IsNullOrEmpty(platform))
            return new(Array.Empty<MethodCheck>(), new[] {"Platform reference assemblies unavailable"});
        var references = platform.Split(System.IO.Path.PathSeparator)
            .Select(x => MetadataReference.CreateFromFile(x)).Cast<MetadataReference>().ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(SharpContracts.VerifyAttribute).Assembly.Location));
        var compilation = CSharpCompilation.Create("SharpContracts.InlineProof",
            new[] {tree}, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                checkOverflow: false));
        errors.AddRange(compilation.GetDiagnostics().Where(x=>x.Severity==DiagnosticSeverity.Error)
            .Select(x=>x.ToString()));
        if (errors.Count != 0) return new(Array.Empty<MethodCheck>(), errors);

        var model = compilation.GetSemanticModel(tree);
        var candidates = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(x=>x.Identifier.ValueText==methodName).ToArray();
        if (candidates.Length!=1)
            return new(Array.Empty<MethodCheck>(),new[]{"Expected exactly one named method"});
        var method = candidates[0];
        var symbol = model.GetDeclaredSymbol(method);
        if (symbol is null || !symbol.GetAttributes().Any(a =>
                RuntimeSymbolPolicy.IsRuntimeType(a.AttributeClass,"SharpContracts.VerifyAttribute")))
            return new(Array.Empty<MethodCheck>(),new[]{"Method must have [Verify] attribute"});
        if (method.Body is null || symbol.ReturnType.SpecialType!=SpecialType.System_UInt32 ||
            symbol.IsAsync || symbol.ReturnsByRef || symbol.ReturnsByRefReadonly ||
            symbol.Parameters.Any(p=>p.Type.SpecialType!=SpecialType.System_UInt32 || p.RefKind!=RefKind.None))
            return new(Array.Empty<MethodCheck>(),
                new[]{"Supported subset: synchronous uint-return method with uint parameters and block body"});
        try
        {
            using var ctx = new Context();
            using var solver = ctx.MkSolver();
            using var options = ctx.MkParams();
            options.Add("timeout",10000u);
            solver.Parameters=options;
            var body=model.GetOperation(method.Body) as IBlockOperation
                ?? throw new NotSupportedException("Unbound method body");
            checks.AddRange(new InlineControlFlow(ctx,solver,symbol).Verify(body));
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException or Z3Exception or InvalidOperationException)
        {
            errors.Add(ex.Message);
        }
        return errors.Count==0?new(checks,errors):new(Array.Empty<MethodCheck>(),errors);
    }
}
