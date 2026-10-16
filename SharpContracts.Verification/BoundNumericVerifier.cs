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
            var parameters = new Dictionary<ISymbol,BitVecExpr>(SymbolEqualityComparer.Default);
            foreach(var p in method[0].ParameterList.Parameters)
            {
                if(semantic.GetDeclaredSymbol(p) is not IParameterSymbol symbol ||
                   symbol.RefKind!=RefKind.None || p.Default is not null)
                    throw new NotSupportedException("Only ordinary parameters supported");
                var kind=NumericTypeDescriptor.FromSymbol(symbol.Type);
                if(kind.Kind is not (NumericModelKind.SignedBitVector or NumericModelKind.UnsignedBitVector) ||
                    kind.Width is not (32 or 64 or 16 or 8))
                    throw new NotSupportedException("Unsupported parameter type: "+symbol.Type);
                parameters.Add(symbol,ctx.MkBVConst(symbol.Name,(uint)kind.Width.Value));
            }
            bool hasEnsure=false;
            foreach(var statement in method[0].Body.Statements)
            {
                if(statement is not ExpressionStatementSyntax {Expression: InvocationExpressionSyntax call} ||
                   semantic.GetOperation(call) is not IInvocationOperation invocation ||
                   invocation.TargetMethod.ContainingType.ToDisplayString()!="SharpContracts.Contract" ||
                   invocation.TargetMethod.Name is not ("Require" or "Ensure"))
                    throw new NotSupportedException("Only Contract.Require/Ensure statements are supported");
                var inputs=invocation.Arguments.ToDictionary(x=>x.Parameter!.Name,x=>x.Value);
                if(!inputs.TryGetValue("condition",out var condition) ||
                   !inputs.TryGetValue("id",out var id) || id.ConstantValue.Value is not string label ||
                   !inputs.TryGetValue("message",out var message))
                    throw new NotSupportedException("Contract must have constant ID and all arguments");
                if(hasEnsure && invocation.TargetMethod.Name=="Require")
                    throw new NotSupportedException("Require after Ensure unsupported");
                var lowered=Lower(condition,ctx,parameters);
                if(lowered is not BoolExpr predicate) throw new NotSupportedException("Expected Boolean condition");
                if(invocation.TargetMethod.Name=="Require") solver.Assert(predicate);
                else
                {
                    hasEnsure=true;
                    solver.Push();
                    solver.Assert(ctx.MkNot(predicate));
                    var status=solver.Check();
                    results.Add(new MethodCheck(label,
                        tree.GetLineSpan(statement.Span).StartLinePosition.Line+1,
                        status==Status.UNSATISFIABLE?VerificationStatus.Proved:
                        status==Status.SATISFIABLE?VerificationStatus.Counterexample:VerificationStatus.Unknown,
                        status==Status.SATISFIABLE?solver.Model.ToString():null,
                        status==Status.UNKNOWN?solver.ReasonUnknown:null));
                    solver.Pop();
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

    private static Expr Lower(IOperation op,Context c,Dictionary<ISymbol,BitVecExpr> vars)
    {
        if(op is IConversionOperation convert)
        {
            var operand=Lower(convert.Operand,c,vars);
            if(convert.Type is null || operand is not BitVecExpr bv)
                throw new NotSupportedException("Unsupported conversion");
            if(convert.OperatorMethod is not null || (!convert.Conversion.IsNumeric && !convert.Conversion.IsIdentity))
                throw new NotSupportedException("User-defined or nonnumeric conversion");
            var source=NumericTypeDescriptor.FromSymbol(convert.Operand.Type!);
            var dest=NumericTypeDescriptor.FromSymbol(convert.Type);
            if(source.Kind is not (NumericModelKind.SignedBitVector or NumericModelKind.UnsignedBitVector) ||
               dest.Kind is not (NumericModelKind.SignedBitVector or NumericModelKind.UnsignedBitVector) ||
               source.Width is null || dest.Width is null)
                throw new NotSupportedException("Conversion type unsupported");
            uint from=(uint)source.Width.Value, to=(uint)dest.Width.Value;
            if(convert.IsChecked) throw new NotSupportedException("Checked conversions require exception verification");
            if(to==from) return bv;
            if(to<from) return c.MkExtract(to-1,0,bv);
            return source.Kind==NumericModelKind.SignedBitVector
                ? c.MkSignExt(to-from,bv) : c.MkZeroExt(to-from,bv);
        }
        if(op is IParameterReferenceOperation p && vars.TryGetValue(p.Parameter,out var value)) return value;
        if(op is ILiteralOperation lit && lit.Type is not null)
        {
            if(lit.ConstantValue.Value is bool b) return c.MkBool(b);
            var d=NumericTypeDescriptor.FromSymbol(lit.Type);
            if(d.Width is null || d.Kind is not (NumericModelKind.SignedBitVector or NumericModelKind.UnsignedBitVector))
                throw new NotSupportedException("Unsupported literal");
            // Converting an unsigned 64-bit literal through Int64 throws for values above long.MaxValue.
            // Preserve its raw two's-complement bit pattern instead.
            ulong bits = lit.ConstantValue.Value switch
            {
                ulong n => n,
                long n => unchecked((ulong)n),
                uint n => n,
                int n => unchecked((ulong)n),
                ushort n => n,
                short n => unchecked((ulong)n),
                byte n => n,
                sbyte n => unchecked((ulong)n),
                char n => n,
                _ => throw new NotSupportedException("Unsupported integer literal value")
            };
            return c.MkBV(bits,(uint)d.Width.Value);
        }
        if(op is IUnaryOperation unary)
        {
            if(unary.OperatorMethod is not null) throw new NotSupportedException("Custom unary operator");
            var operand=Lower(unary.Operand,c,vars);
            return unary.OperatorKind switch {
                UnaryOperatorKind.Not when operand is BoolExpr b=>c.MkNot(b),
                _=>throw new NotSupportedException("Unary operator unsupported")
            };
        }
        if(op is IBinaryOperation binary)
        {
            if(binary.OperatorMethod is not null)
                throw new NotSupportedException("Unverified user-defined operator "+binary.OperatorMethod);
            if(binary.IsChecked) throw new NotSupportedException("Checked arithmetic requires overflow proof");
            var left=Lower(binary.LeftOperand,c,vars);
            var right=Lower(binary.RightOperand,c,vars);
            if(binary.OperatorKind==BinaryOperatorKind.ConditionalAnd)
                return c.MkAnd((BoolExpr)left,(BoolExpr)right);
            if(binary.OperatorKind==BinaryOperatorKind.ConditionalOr)
                return c.MkOr((BoolExpr)left,(BoolExpr)right);
            if(binary.OperatorKind==BinaryOperatorKind.Equals) return c.MkEq(left,right);
            if(binary.OperatorKind==BinaryOperatorKind.NotEquals) return c.MkNot(c.MkEq(left,right));
            if(left is not BitVecExpr a || right is not BitVecExpr b)
                throw new NotSupportedException("Expected bit-vector operands");
            var type=NumericTypeDescriptor.FromSymbol(binary.LeftOperand.Type!);
            if (type.Kind is not (NumericModelKind.SignedBitVector or NumericModelKind.UnsignedBitVector))
                throw new NotSupportedException("Comparison requires supported integral type");
            bool signed=type.Kind==NumericModelKind.SignedBitVector;
            return binary.OperatorKind switch {
                BinaryOperatorKind.Add=>c.MkBVAdd(a,b),
                BinaryOperatorKind.Subtract=>c.MkBVSub(a,b),
                BinaryOperatorKind.GreaterThan=>signed?c.MkBVSGT(a,b):c.MkBVUGT(a,b),
                BinaryOperatorKind.GreaterThanOrEqual=>signed?c.MkBVSGE(a,b):c.MkBVUGE(a,b),
                BinaryOperatorKind.LessThan=>signed?c.MkBVSLT(a,b):c.MkBVULT(a,b),
                BinaryOperatorKind.LessThanOrEqual=>signed?c.MkBVSLE(a,b):c.MkBVULE(a,b),
                _=>throw new NotSupportedException("Unsupported binary operation "+binary.OperatorKind)
            };
        }
        throw new NotSupportedException("Unsupported bound operation "+op.Kind);
    }
}
