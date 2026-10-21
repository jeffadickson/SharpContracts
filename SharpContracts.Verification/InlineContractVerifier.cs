using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Z3;
using SharpContracts.Solver;

namespace SharpContracts.Verification;

/// <summary>
/// Proof engine for [Verify] methods with uint parameters, locals, inline contracts,
/// and a single final return. Arithmetic is CLR unchecked BV32; unsupported syntax fails closed.
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
                a.AttributeClass?.ToDisplayString()=="SharpContracts.VerifyAttribute"))
            return new(Array.Empty<MethodCheck>(),new[]{"Method must have [Verify] attribute"});
        if (method.Body is null || symbol.ReturnType.SpecialType!=SpecialType.System_UInt32 ||
            symbol.IsAsync || symbol.Parameters.Any(p=>p.Type.SpecialType!=SpecialType.System_UInt32))
            return new(Array.Empty<MethodCheck>(),
                new[]{"Supported subset: synchronous uint-return method with uint parameters and block body"});
        try
        {
            using var ctx = new Context();
            using var solver = ctx.MkSolver();
            using var options = ctx.MkParams();
            options.Add("timeout",10000u);
            solver.Parameters=options;
            var values = new Dictionary<ISymbol,BitVecExpr>(SymbolEqualityComparer.Default);
            foreach(var p in symbol.Parameters)
                values.Add(p,ctx.MkBVConst(p.Name,32));

            Microsoft.Z3.Expr Lower(IOperation operation)
            {
                if (operation is IParenthesizedOperation parenthesized)
                    return Lower(parenthesized.Operand);
                if (operation is IConversionOperation conversion)
                {
                    if (conversion.IsChecked || conversion.OperatorMethod is not null ||
                        !conversion.Conversion.IsIdentity)
                        throw new NotSupportedException("Nonidentity or checked conversion unsupported");
                    return Lower(conversion.Operand);
                }
                if (operation is IParameterReferenceOperation param &&
                    values.TryGetValue(param.Parameter,out var pv)) return pv;
                if (operation is ILocalReferenceOperation local &&
                    values.TryGetValue(local.Local,out var lv)) return lv;
                if (operation is ILiteralOperation literal)
                {
                    if (literal.ConstantValue.Value is uint u) return ctx.MkBV(u,32);
                    if (literal.ConstantValue.Value is bool b) return ctx.MkBool(b);
                    throw new NotSupportedException("Only uint and bool literals supported");
                }
                if (operation is IBinaryOperation binary)
                {
                    if(binary.IsChecked || binary.OperatorMethod is not null)
                        throw new NotSupportedException("Checked or overloaded binary operation unsupported");
                    // The supported operators are pure and total over BV32.
                    // SMT bit-vector division is total; C# division by zero throws.
                    // Admit only a nonzero compile-time UInt32 divisor until exceptional
                    // proof obligations are implemented.
                    if(binary.OperatorKind==BinaryOperatorKind.Divide)
                    {
                        if(binary.RightOperand.ConstantValue.Value is not uint divisor || divisor==0u)
                            throw new NotSupportedException("Division requires nonzero constant uint divisor");
                    }
                    var l=Lower(binary.LeftOperand);
                    var r=Lower(binary.RightOperand);
                    if (binary.OperatorKind is BinaryOperatorKind.Equals) return ctx.MkEq(l,r);
                    if (binary.OperatorKind is BinaryOperatorKind.NotEquals) return ctx.MkNot(ctx.MkEq(l,r));
                    if (l is not BitVecExpr a || r is not BitVecExpr b ||
                        a.SortSize != 32 || b.SortSize != 32)
                        throw new NotSupportedException("Binary operation requires BV32 operands");
                    return binary.OperatorKind switch
                    {
                        BinaryOperatorKind.Add => ctx.MkBVAdd(a,b),
                        BinaryOperatorKind.Subtract => ctx.MkBVSub(a,b),
                        BinaryOperatorKind.Divide => ctx.MkBVUDiv(a,b),
                        BinaryOperatorKind.LessThan => ctx.MkBVULT(a,b),
                        BinaryOperatorKind.LessThanOrEqual => ctx.MkBVULE(a,b),
                        BinaryOperatorKind.GreaterThan => ctx.MkBVUGT(a,b),
                        BinaryOperatorKind.GreaterThanOrEqual => ctx.MkBVUGE(a,b),
                        _ => throw new NotSupportedException("Unsupported operator "+binary.OperatorKind)
                    };
                }
                throw new NotSupportedException("Unsupported operation "+operation.Kind);
            }

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
                    var value=Lower(op) as BitVecExpr
                        ?? throw new NotSupportedException("Local initializer not BV32");
                    values.Add(localSymbol,value);
                }
                else if (statement is ExpressionStatementSyntax {Expression: InvocationExpressionSyntax invocation} &&
                    model.GetOperation(invocation) is IInvocationOperation call &&
                    call.TargetMethod.ContainingType.ToDisplayString()=="SharpContracts.Contract" &&
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
                    var predicate=Lower(condition) as BoolExpr
                        ?? throw new NotSupportedException("Contract predicate must be Bool");
                    if(call.TargetMethod.Name=="Require") solver.Assert(predicate);
                    else
                    {
                        seenNonrequire=true;
                        if(solver.Check()!=Status.SATISFIABLE)
                            throw new NotSupportedException("Preconditions inconsistent or solver unknown");
                        solver.Push();
                        solver.Assert(ctx.MkNot(predicate));
                        var answer=solver.Check();
                        checks.Add(new(contractId,line,
                            answer==Status.UNSATISFIABLE?VerificationStatus.Proved:
                            answer==Status.SATISFIABLE?VerificationStatus.Counterexample:VerificationStatus.Unknown,
                            answer==Status.SATISFIABLE?solver.Model.ToString():null,
                            answer==Status.UNKNOWN?solver.ReasonUnknown:null));
                        solver.Pop();
                        solver.Assert(predicate);
                    }
                }
                else if (statement is ReturnStatementSyntax {Expression: not null} ret)
                {
                    var operation=(model.GetOperation(ret) as IReturnOperation)?.ReturnedValue
                        ?? throw new NotSupportedException("Unbound return expression");
                    if(Lower(operation) is not BitVecExpr)
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
