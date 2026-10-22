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
/// and a single final return. Expressions use compiler-resolved integral bit vectors;
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
            var values = new Dictionary<ISymbol,ClrExpression>(SymbolEqualityComparer.Default);
            foreach(var p in symbol.Parameters)
                values.Add(p,new ClrParameter(p.Name, ClrOperationLowerer.ScalarType(p.Type)));
            var lowerer = new ClrOperationLowerer(values);
            var encoder = new ClrExpressionEncoder(ctx);

            bool seenNonrequire = false;
            bool returned = false;
            foreach(var statement in method.Body.Statements)
            {
                int line=tree.GetLineSpan(statement.Span).StartLinePosition.Line+1;
                if (returned) throw new NotSupportedException("Statements after return unsupported");
                if (statement is LocalDeclarationStatementSyntax localStatement)
                {
                    seenNonrequire=true;
                    if (localStatement.Declaration.Variables.Count!=1)
                        throw new NotSupportedException("Exactly one local per declaration");
                    var variable=localStatement.Declaration.Variables[0];
                    if (variable.Initializer is null ||
                        model.GetDeclaredSymbol(variable) is not ILocalSymbol localSymbol ||
                        localSymbol.Type.SpecialType!=SpecialType.System_UInt32)
                        throw new NotSupportedException("Initialized uint local required");
                    var op=(model.GetOperation(variable.Initializer) as IVariableInitializerOperation)?.Value
                        ?? throw new NotSupportedException("Unbound local initializer");
                    var value=lowerer.Lower(op);
                    if (value.ValueType != ClrScalarType.Integral(32, false))
                        throw new NotSupportedException("Local initializer not UInt32");
                    encoder.Encode(value);
                    values.Add(localSymbol,value);
                }
                else if (statement is ExpressionStatementSyntax {Expression: InvocationExpressionSyntax invocation} &&
                    model.GetOperation(invocation) is IInvocationOperation call &&
                    RuntimeSymbolPolicy.IsRuntimeType(call.TargetMethod.ContainingType,"SharpContracts.Contract") &&
                    call.TargetMethod.Name is ("Require" or "Ensure" or "Assert"))
                {
                    var args=call.Arguments.ToDictionary(a=>a.Parameter!.Name,a=>a.Value);
                    if(!args.TryGetValue("condition",out var condition) ||
                       !args.TryGetValue("id",out var id) ||
                       id.ConstantValue.Value is not string contractId ||
                       string.IsNullOrWhiteSpace(contractId) ||
                       !args.TryGetValue("message",out var message) ||
                       message.ConstantValue.Value is not string)
                        throw new NotSupportedException("Constant ID/message required");
                    if (call.TargetMethod.Name=="Require" && seenNonrequire)
                        throw new NotSupportedException("Require must occur before executable statements");
                    var predicate=encoder.Encode(lowerer.Lower(condition)) as BoolExpr
                        ?? throw new NotSupportedException("Contract predicate must be Bool");
                    if(call.TargetMethod.Name=="Require") solver.Assert(predicate);
                    else
                    {
                        seenNonrequire=true;
                        checks.Add(ProofQuery.Check(ctx,solver,predicate,contractId,line,
                            checks.Count != 0, values.Where(p=>p.Key is IParameterSymbol),encoder));
                        solver.Assert(predicate);
                    }
                }
                else if (statement is ReturnStatementSyntax {Expression: not null} ret)
                {
                    var operation=(model.GetOperation(ret) as IReturnOperation)?.ReturnedValue
                        ?? throw new NotSupportedException("Unbound return expression");
                    if(encoder.Encode(lowerer.Lower(operation)) is not BitVecExpr)
                        throw new NotSupportedException("Return value must be BV32");
                    returned=true;
                }
                else throw new NotSupportedException("Unsupported statement at line "+line+": "+statement.Kind());
            }
            if(!returned) throw new NotSupportedException("Final return statement required");
            if(checks.Count==0) throw new NotSupportedException("At least one Ensure or Assert required");
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException or Z3Exception or InvalidOperationException)
        {
            errors.Add(ex.Message);
        }
        return errors.Count==0?new(checks,errors):new(Array.Empty<MethodCheck>(),errors);
    }
}
