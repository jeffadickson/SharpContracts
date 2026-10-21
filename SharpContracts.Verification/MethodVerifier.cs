using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SharpContracts.Symbolic;
using SharpContracts.Solver;

namespace SharpContracts.Verification;

public sealed record MethodCheck(string ContractId, int Line, VerificationStatus Status, string? Counterexample, string? Reason,
    IReadOnlyDictionary<string, string>? CounterexampleInputs = null);
public sealed record MethodVerification(IReadOnlyList<MethodCheck> Checks, IReadOnlyList<string> Errors)
{
    public bool AllProved => Errors.Count == 0 && Checks.Count > 0 &&
                              Checks.All(c => c.Status == VerificationStatus.Proved);
}

/// <summary>
/// A restricted mathematical-integer path verifier, NOT a CLR overflow proof.
/// Fail-closed: any unsupported statement or parse error clears all proof results.
/// </summary>
public static class MethodVerifier
{
    private sealed class PathState
    {
        public Dictionary<string, ContractExpr> Values { get; }
        public List<ContractExpr> Assumptions { get; }
        public bool HasEnsures { get; set; }
        public PathState(Dictionary<string, ContractExpr> values, List<ContractExpr> assumptions)
        {
            Values = values; Assumptions = assumptions;
        }
        public PathState Clone() => new(new(Values, StringComparer.Ordinal), new(Assumptions))
        { HasEnsures = HasEnsures };
    }

    public static MethodVerification Verify(string source, string methodName)
    {
        var errors = new List<string>();
        var checks = new List<MethodCheck>();
        var tree = CSharpSyntaxTree.ParseText(source);
        foreach (var diagnostic in tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
            errors.Add("C# syntax: " + diagnostic);
        if (errors.Count != 0) return new(Array.Empty<MethodCheck>(), errors);
        var methods = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.ValueText == methodName).ToList();
        if (methods.Count != 1) return new(Array.Empty<MethodCheck>(),
            new[] { "Expected exactly one method named " + methodName });
        var method = methods[0];
        if (method.Body is null || method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)) ||
            method.ReturnType.ToString() != "void")
            return new(Array.Empty<MethodCheck>(),
                new[] { "Only synchronous void block-bodied methods are supported" });

        var initial = new Dictionary<string, ContractExpr>(StringComparer.Ordinal);
        foreach (var p in method.ParameterList.Parameters)
        {
            if (p.Type?.ToString() != "long" || p.Default is not null ||
                p.Modifiers.Count != 0 || initial.ContainsKey(p.Identifier.ValueText))
                errors.Add("Only distinct ordinary long parameters supported: " + p.Identifier.ValueText);
            else initial.Add(p.Identifier.ValueText, Expr.Param(p.Identifier.ValueText, SymbolicType.Integer));
        }
        if (errors.Count != 0) return new(Array.Empty<MethodCheck>(), errors);
        try
        {
            var start = new PathState(initial, new List<ContractExpr>());
            Walk(method.Body.Statements, new List<PathState> { start }, source, checks);
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException or OverflowException)
        {
            errors.Add(ex.Message);
        }
        // Do not surface a partial proof after an unsupported statement.
        return errors.Count > 0
            ? new(Array.Empty<MethodCheck>(), errors)
            : new(checks, errors);
    }

    private static List<PathState> Walk(SyntaxList<StatementSyntax> statements,
        List<PathState> paths, string source, List<MethodCheck> checks)
    {
        foreach (var statement in statements)
        {
            var next = new List<PathState>();
            foreach (var path in paths)
            {
                try
                {
                    switch (statement)
                    {
                        case BlockSyntax block:
                            next.AddRange(Walk(block.Statements, new List<PathState> { path }, source, checks));
                            break;
                        case IfStatementSyntax branch:
                        {
                            var predicate = Lower(branch.Condition, path.Values);
                            if (predicate.Type != SymbolicType.Boolean) throw new NotSupportedException("If condition must be Boolean");
                            var yes = path.Clone();
                            yes.Assumptions.Add(predicate);
                            next.AddRange(Walk(AsStatements(branch.Statement), new List<PathState> {yes}, source, checks));
                            var no = path.Clone();
                            no.Assumptions.Add(Expr.Not(predicate));
                            next.AddRange(branch.Else is null ? new List<PathState> {no} :
                                Walk(AsStatements(branch.Else.Statement), new List<PathState> {no}, source, checks));
                            break;
                        }
                        case LocalDeclarationStatementSyntax local:
                            if (local.Declaration.Type.ToString() is not ("long" or "var"))
                                throw new NotSupportedException("Only long or var locals supported");
                            foreach (var v in local.Declaration.Variables)
                            {
                                if (v.Initializer is null || path.Values.ContainsKey(v.Identifier.ValueText))
                                    throw new NotSupportedException("Local must be unique and initialized");
                                path.Values[v.Identifier.ValueText] = Lower(v.Initializer.Value, path.Values);
                            }
                            next.Add(path);
                            break;
                        case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assign }
                            when assign.IsKind(SyntaxKind.SimpleAssignmentExpression) &&
                                 assign.Left is IdentifierNameSyntax lhs &&
                                 path.Values.ContainsKey(lhs.Identifier.ValueText):
                            path.Values[lhs.Identifier.ValueText] = Lower(assign.Right, path.Values);
                            next.Add(path);
                            break;
                        case ExpressionStatementSyntax { Expression: InvocationExpressionSyntax call }
                            when call.Expression.ToString() is "Contract.Require" or "Contract.Ensure":
                        {
                            var args = call.ArgumentList.Arguments;
                            if (args.Count != 3 || args.Any(a => a.NameColon is not null) ||
                                args[1].Expression is not LiteralExpressionSyntax id ||
                                !id.IsKind(SyntaxKind.StringLiteralExpression))
                                throw new NotSupportedException("Contract needs three positional arguments and literal ID");
                            var condition = Lower(args[0].Expression, path.Values);
                            if (condition.Type != SymbolicType.Boolean)
                                throw new NotSupportedException("Contract condition must be Boolean");
                            if (call.Expression.ToString() == "Contract.Require")
                            {
                                if (path.HasEnsures) throw new NotSupportedException("Require after Ensure unsupported");
                                // Require has fail-fast semantics; continue only on condition true.
                                path.Assumptions.Add(condition);
                            }
                            else
                            {
                                var result = Z3Verifier.Verify(path.Assumptions, condition);
                                checks.Add(new(id.Token.ValueText, Line(source, statement.SpanStart),
                                    result.Status, result.Model, result.Reason));
                                // On normal return from Ensure the condition holds.
                                path.Assumptions.Add(condition);
                                path.HasEnsures = true;
                            }
                            next.Add(path);
                            break;
                        }
                        default: throw new NotSupportedException("Unsupported statement: " + statement.Kind());
                    }
                }
                catch (Exception ex) when (ex is NotSupportedException or ArgumentException or OverflowException)
                {
                    throw new NotSupportedException("Line " + Line(source, statement.SpanStart) + ": " + ex.Message, ex);
                }
            }
            paths = next;
            if (paths.Count > 256) throw new NotSupportedException("Path limit exceeded (256)");
        }
        return paths;
    }

    private static SyntaxList<StatementSyntax> AsStatements(StatementSyntax s) =>
        s is BlockSyntax b ? b.Statements : SyntaxFactory.List(new[] { s });

    private static int Line(string source, int position) => source[..position].Count(c => c == '\n') + 1;

    private static ContractExpr Lower(ExpressionSyntax expression, IReadOnlyDictionary<string, ContractExpr> state)
    {
        if (expression is ParenthesizedExpressionSyntax p) return Lower(p.Expression, state);
        if (expression is IdentifierNameSyntax name && state.TryGetValue(name.Identifier.ValueText, out var value))
            return value;
        if (expression is LiteralExpressionSyntax lit)
        {
            if (lit.IsKind(SyntaxKind.TrueLiteralExpression)) return Expr.Bool(true);
            if (lit.IsKind(SyntaxKind.FalseLiteralExpression)) return Expr.Bool(false);
            if (lit.IsKind(SyntaxKind.NumericLiteralExpression))
            {
                if (lit.Token.Value is int i) return Expr.Int(i);
                if (lit.Token.Value is long l) return Expr.Int(l);
            }
        }
        if (expression is PrefixUnaryExpressionSyntax unary)
        {
            if (unary.IsKind(SyntaxKind.UnaryMinusExpression))
                return Expr.Subtract(Expr.Int(0), Lower(unary.Operand, state));
            if (unary.IsKind(SyntaxKind.LogicalNotExpression))
                return Expr.Not(Lower(unary.Operand, state));
        }
        if (expression is BinaryExpressionSyntax b)
        {
            var a = Lower(b.Left, state);
            var c = Lower(b.Right, state);
            return b.Kind() switch
            {
                SyntaxKind.AddExpression => Expr.Add(a,c),
                SyntaxKind.SubtractExpression => Expr.Subtract(a,c),
                SyntaxKind.GreaterThanExpression => Expr.Gt(a,c),
                SyntaxKind.GreaterThanOrEqualExpression => Expr.Ge(a,c),
                SyntaxKind.LessThanExpression => Expr.Lt(a,c),
                SyntaxKind.LessThanOrEqualExpression => Expr.Le(a,c),
                SyntaxKind.EqualsExpression => Expr.Eq(a,c),
                SyntaxKind.NotEqualsExpression => Expr.Ne(a,c),
                SyntaxKind.LogicalAndExpression => Expr.And(a,c),
                SyntaxKind.LogicalOrExpression => Expr.Or(a,c),
                _ => throw new NotSupportedException("Unsupported expression: " + b.Kind())
            };
        }
        throw new NotSupportedException("Unsupported expression: " + expression.Kind());
    }
}
