using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SharpContracts.Symbolic;
using SharpContracts.Solver;

namespace SharpContracts.Verification;

public sealed record MethodCheck(string ContractId, int Line, VerificationStatus Status, string? Counterexample, string? Reason);
public sealed record MethodVerification(IReadOnlyList<MethodCheck> Checks, IReadOnlyList<string> Errors);

/// <summary>
/// Sound only for the supported ideal-integer, straight-line fragment.
/// Unsupported syntax fails closed; this does not model CLR overflow.
/// </summary>
public static class MethodVerifier
{
    public static MethodVerification Verify(string source, string methodName)
    {
        var errors = new List<string>();
        var checks = new List<MethodCheck>();
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.ValueText == methodName).ToList();
        if (methods.Count != 1) return new(checks, new[] { "Expected exactly one method named " + methodName });
        var method = methods[0];
        if (method.Body is null || method.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AsyncKeyword)))
            return new(checks, new[] { "Only synchronous block-bodied methods are supported" });
        var state = new Dictionary<string, ContractExpr>(StringComparer.Ordinal);
        var assumptions = new List<ContractExpr>();
        foreach (var p in method.ParameterList.Parameters)
        {
            if (p.Type?.ToString() != "long")
                errors.Add("Only long parameters supported: " + p.Identifier.ValueText);
            else state[p.Identifier.ValueText] = Expr.Param(p.Identifier.ValueText, SymbolicType.Integer);
        }
        if (errors.Count != 0) return new(checks, errors);
        bool sawEnsure = false;
        bool sawReturn = false;
        foreach (var statement in method.Body.Statements)
        {
            try
            {
                if (sawReturn) throw new NotSupportedException("Statements after return are unsupported");
                if (statement is LocalDeclarationStatementSyntax local)
                {
                    if (local.Declaration.Type.ToString() != "long" && local.Declaration.Type.ToString() != "var")
                        throw new NotSupportedException("Only long or var locals supported");
                    foreach (var v in local.Declaration.Variables)
                    {
                        if (v.Initializer is null || state.ContainsKey(v.Identifier.ValueText))
                            throw new NotSupportedException("Local requires initializer and unique name");
                        state[v.Identifier.ValueText] = Lower(v.Initializer.Value, state);
                    }
                }
                else if (statement is ExpressionStatementSyntax {Expression: AssignmentExpressionSyntax assignment} &&
                         assignment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SimpleAssignmentExpression) &&
                         assignment.Left is IdentifierNameSyntax target && state.ContainsKey(target.Identifier.ValueText))
                {
                    state[target.Identifier.ValueText] = Lower(assignment.Right, state);
                }
                else if (statement is ExpressionStatementSyntax {Expression: InvocationExpressionSyntax invocation} &&
                         invocation.Expression.ToString() is "Contract.Require" or "Contract.Ensure")
                {
                    var name = invocation.Expression.ToString();
                    var args = invocation.ArgumentList.Arguments;
                    if (args.Count < 2 || args[1].Expression is not LiteralExpressionSyntax id ||
                        !id.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression))
                        throw new NotSupportedException("Contract calls require positional condition and literal ID");
                    var condition = Lower(args[0].Expression, state);
                    if (condition.Type != SymbolicType.Boolean) throw new NotSupportedException("Contract condition must be Boolean");
                    if (name == "Contract.Require")
                    {
                        if (sawEnsure) throw new NotSupportedException("Preconditions must precede ensures");
                        assumptions.Add(condition);
                    }
                    else
                    {
                        sawEnsure = true;
                        var result = Z3Verifier.Verify(assumptions, condition);
                        checks.Add(new(id.Token.ValueText,
                            source[..statement.SpanStart].Count(c => c == '\n') + 1,
                            result.Status, result.Model, result.Reason));
                    }
                }
                else if (statement is ReturnStatementSyntax {Expression: null}) sawReturn = true;
                else throw new NotSupportedException("Unsupported statement: " + statement.Kind());
            }
            catch (Exception ex) when (ex is NotSupportedException or ArgumentException or OverflowException)
            {
                errors.Add("Line " + (source[..statement.SpanStart].Count(c => c == '\n') + 1) + ": " + ex.Message);
                break;
            }
        }
        return new(checks, errors);
    }

    private static ContractExpr Lower(ExpressionSyntax expression, IReadOnlyDictionary<string, ContractExpr> state)
    {
        if (expression is ParenthesizedExpressionSyntax p) return Lower(p.Expression, state);
        if (expression is IdentifierNameSyntax name && state.TryGetValue(name.Identifier.ValueText, out var value))
            return value;
        if (expression is LiteralExpressionSyntax lit && lit.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NumericLiteralExpression))
        {
            if (lit.Token.Value is int i) return Expr.Int(i);
            if (lit.Token.Value is long l) return Expr.Int(l);
        }
        if (expression is PrefixUnaryExpressionSyntax unary &&
            unary.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.UnaryMinusExpression))
            return Expr.Subtract(Expr.Int(0), Lower(unary.Operand, state));
        if (expression is BinaryExpressionSyntax b)
        {
            var a = Lower(b.Left, state);
            var c = Lower(b.Right, state);
            return b.Kind() switch
            {
                Microsoft.CodeAnalysis.CSharp.SyntaxKind.AddExpression => Expr.Add(a,c),
                Microsoft.CodeAnalysis.CSharp.SyntaxKind.SubtractExpression => Expr.Subtract(a,c),
                Microsoft.CodeAnalysis.CSharp.SyntaxKind.GreaterThanExpression => Expr.Gt(a,c),
                Microsoft.CodeAnalysis.CSharp.SyntaxKind.GreaterThanOrEqualExpression => Expr.Ge(a,c),
                Microsoft.CodeAnalysis.CSharp.SyntaxKind.LessThanExpression => Expr.Lt(a,c),
                Microsoft.CodeAnalysis.CSharp.SyntaxKind.LessThanOrEqualExpression => Expr.Le(a,c),
                Microsoft.CodeAnalysis.CSharp.SyntaxKind.EqualsExpression => Expr.Eq(a,c),
                Microsoft.CodeAnalysis.CSharp.SyntaxKind.NotEqualsExpression => Expr.Ne(a,c),
                Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalAndExpression => Expr.And(a,c),
                Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalOrExpression => Expr.Or(a,c),
                _ => throw new NotSupportedException("Unsupported expression: " + b.Kind())
            };
        }
        throw new NotSupportedException("Unsupported expression: " + expression.Kind());
    }
}
