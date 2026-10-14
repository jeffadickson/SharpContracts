using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;
using SharpContracts.Solver;

namespace SharpContracts.Verification;

/// <summary>Verification of a restricted C# long subset with simple merged if/else assignments using signed BV64.</summary>
public static class BitVectorMethodVerifier
{
    public static MethodVerification Verify(string source, string methodName)
    {
        var errors = new List<string>();
        var checks = new List<MethodCheck>();
        var tree = CSharpSyntaxTree.ParseText(source);
        errors.AddRange(tree.GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error).Select(x => x.ToString()));
        if (errors.Count != 0) return new(Array.Empty<MethodCheck>(), errors);
        var methods = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(x => x.Identifier.ValueText == methodName).ToArray();
        if (methods.Length != 1 || methods[0].Body is null ||
            methods[0].ReturnType.ToString() != "void" ||
            methods[0].Modifiers.Any(x => x.IsKind(SyntaxKind.AsyncKeyword)))
            return new(Array.Empty<MethodCheck>(), new[] { "Requires one synchronous void block method" });
        var method = methods[0];
        var state = new Dictionary<string, string>(StringComparer.Ordinal);
        var assumptions = new List<string>();
        var overflowConditions = new List<(string Formula,int Line,string[] Assumptions)>();
        bool startedEnsures = false;
        try
        {
            foreach (var p in method.ParameterList.Parameters)
            {
                var name = p.Identifier.ValueText;
                if (p.Type?.ToString() != "long" || p.Default is not null || p.Modifiers.Count != 0 ||
                    !IsIdentifier(name) || state.ContainsKey(name))
                    throw new NotSupportedException("Only ordinary distinct long parameters are supported");
                state[name] = "|" + name + "|";
            }

            string Lower(ExpressionSyntax e, bool isChecked, int line)
            {
                if (e is ParenthesizedExpressionSyntax par) return Lower(par.Expression, isChecked, line);
                if (e is CheckedExpressionSyntax ce)
                    return Lower(ce.Expression, ce.Keyword.IsKind(SyntaxKind.CheckedKeyword), line);
                if (e is IdentifierNameSyntax id && state.TryGetValue(id.Identifier.ValueText, out var val))
                    return val;
                if (e is LiteralExpressionSyntax lit)
                {
                    if (lit.IsKind(SyntaxKind.TrueLiteralExpression)) return "true";
                    if (lit.IsKind(SyntaxKind.FalseLiteralExpression)) return "false";
                    if (lit.IsKind(SyntaxKind.NumericLiteralExpression) && lit.Token.Value is int or long)
                        return Bv(Convert.ToInt64(lit.Token.Value));
                }
                if (e is PrefixUnaryExpressionSyntax pre && pre.IsKind(SyntaxKind.LogicalNotExpression))
                    return "(not " + Lower(pre.Operand,isChecked,line) + ")";
                if (e is BinaryExpressionSyntax b)
                {
                    var l=Lower(b.Left,isChecked,line);
                    var r=Lower(b.Right,isChecked,line);
                    var operation = b.Kind();
                    if (operation is SyntaxKind.AddExpression or SyntaxKind.SubtractExpression)
                    {
                        bool add = operation == SyntaxKind.AddExpression;
                        var result = "(" + (add ? "bvadd" : "bvsub") + " " + l + " " + r + ")";
                        if (isChecked)
                        {
                            const string zero = "(_ bv0 64)";
                            string lp="(bvsge "+l+" "+zero+")", rp="(bvsge "+r+" "+zero+")", op="(bvsge "+result+" "+zero+")";
                            // Overflow detection by signs for signed two's complement addition/subtraction.
                            string overflow = add
                                ? "(or (and "+lp+" "+rp+" (not "+op+")) (and (not "+lp+") (not "+rp+") "+op+"))"
                                : "(or (and "+lp+" (not "+rp+") (not "+op+")) (and (not "+lp+") "+rp+" "+op+"))";
                            overflowConditions.Add((overflow,line,assumptions.ToArray()));
                        }
                        return result;
                    }
                    string? op2 = operation switch
                    {
                        SyntaxKind.GreaterThanExpression => "bvsgt",
                        SyntaxKind.GreaterThanOrEqualExpression => "bvsge",
                        SyntaxKind.LessThanExpression => "bvslt",
                        SyntaxKind.LessThanOrEqualExpression => "bvsle",
                        SyntaxKind.EqualsExpression => "=",
                        SyntaxKind.NotEqualsExpression => "distinct",
                        SyntaxKind.LogicalAndExpression => "and",
                        SyntaxKind.LogicalOrExpression => "or",
                        _ => null
                    };
                    if (op2 is not null) return "("+op2+" "+l+" "+r+")";
                }
                throw new NotSupportedException("Unsupported expression: "+e.Kind());
            }

            foreach (var statement in method.Body.Statements)
            {
                int line = tree.GetLineSpan(statement.Span).StartLinePosition.Line+1;
                switch (statement)
                {
                    case LocalDeclarationStatementSyntax local:
                        if (startedEnsures || local.Declaration.Type.ToString() is not ("long" or "var"))
                            throw new NotSupportedException("Only long/var locals before ensures are supported");
                        foreach (var variable in local.Declaration.Variables)
                        {
                            var name = variable.Identifier.ValueText;
                            if (!IsIdentifier(name) || state.ContainsKey(name) || variable.Initializer is null)
                                throw new NotSupportedException("Unique initialized locals required");
                            state[name] = Lower(variable.Initializer.Value,false,line);
                        }
                        break;
                    case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assign }
                        when assign.IsKind(SyntaxKind.SimpleAssignmentExpression) &&
                             assign.Left is IdentifierNameSyntax id && state.ContainsKey(id.Identifier.ValueText) && !startedEnsures:
                        state[id.Identifier.ValueText] = Lower(assign.Right,false,line);
                        break;
                    // Restricted if/else merge: both branches must assign the same existing long.
                    // Represent the merged value with SMT ite, preserving BV64 wraparound.
                    // Checked expressions are rejected here until branch-local overflow VCs exist.
                    case IfStatementSyntax branch when !startedEnsures:
                    {
                        static AssignmentExpressionSyntax? SingleAssign(StatementSyntax statement)
                        {
                            if (statement is BlockSyntax block)
                            {
                                if (block.Statements.Count != 1) return null;
                                statement = block.Statements[0];
                            }
                            return statement is ExpressionStatementSyntax
                                { Expression: AssignmentExpressionSyntax assignment }
                                && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                                ? assignment : null;
                        }
                        var yes=SingleAssign(branch.Statement);
                        var no=branch.Else is null ? null : SingleAssign(branch.Else.Statement);
                        if (yes?.Left is not IdentifierNameSyntax left ||
                            no?.Left is not IdentifierNameSyntax right ||
                            left.Identifier.ValueText != right.Identifier.ValueText ||
                            !state.ContainsKey(left.Identifier.ValueText))
                            throw new NotSupportedException("if/else requires both branches to assign the same existing long");
                        if (yes.Right.DescendantNodesAndSelf().OfType<CheckedExpressionSyntax>().Any() ||
                            no.Right.DescendantNodesAndSelf().OfType<CheckedExpressionSyntax>().Any())
                            throw new NotSupportedException("Checked arithmetic inside branches is not yet modeled");
                        var condition=Lower(branch.Condition,false,line);
                        var truth=Lower(yes.Right,false,line);
                        var falsity=Lower(no.Right,false,line);
                        state[left.Identifier.ValueText]="(ite "+condition+" "+truth+" "+falsity+")";
                        break;
                    }
                    case ExpressionStatementSyntax { Expression: InvocationExpressionSyntax call }
                        when call.Expression.ToString() is "Contract.Require" or "Contract.Ensure":
                        var args=call.ArgumentList.Arguments;
                        if (args.Count != 3 || args.Any(x => x.NameColon is not null) ||
                            args[1].Expression is not LiteralExpressionSyntax label ||
                            !label.IsKind(SyntaxKind.StringLiteralExpression))
                            throw new NotSupportedException("Requires three positional contract arguments with literal ID");
                        var predicate = Lower(args[0].Expression,false,line);
                        if (call.Expression.ToString() == "Contract.Require")
                        {
                            if(startedEnsures) throw new NotSupportedException("Requires must precede Ensures");
                            assumptions.Add(predicate);
                        }
                        else
                        {
                            startedEnsures=true;
                            checks.Add(Solve(label.Token.ValueText,line,
                                assumptions.Append("(not "+predicate+")"),state.Keys));
                            assumptions.Add(predicate); // normal fall-through after an Ensure
                        }
                        break;
                    default:
                        throw new NotSupportedException("Unsupported statement on line "+line+": "+statement.Kind());
                }
            }
            // A checked overflow must never be silently interpreted as an assumption.
            foreach (var (formula,line,atOperation) in overflowConditions)
                checks.Add(Solve("CHECKED-OVERFLOW",line,atOperation.Append(formula),state.Keys));
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException or Z3Exception)
        {
            errors.Add(ex.Message);
        }
        return errors.Count == 0 ? new(checks, errors) : new(Array.Empty<MethodCheck>(),errors);
    }

    private static MethodCheck Solve(string id,int line,IEnumerable<string> formulas,IEnumerable<string> names)
    {
        using var ctx = new Context();
        using var solver=ctx.MkSolver();
        var declarations=string.Join("\n",names.Distinct().Where(IsIdentifier)
            .Select(n=>"(declare-const |"+n+"| (_ BitVec 64))"));
        string script=declarations+"\n"+string.Join("\n",formulas.Select(f=>"(assert "+f+")"));
        solver.FromString(script);
        return solver.Check() switch
        {
            Status.UNSATISFIABLE=>new(id,line,VerificationStatus.Proved,null,null),
            Status.SATISFIABLE=>new(id,line,VerificationStatus.Counterexample,solver.Model.ToString(),null),
            _=>new(id,line,VerificationStatus.Unknown,null,solver.ReasonUnknown)
        };
    }
    private static bool IsIdentifier(string s)=> s.Length>0 && (char.IsLetter(s[0])||s[0]=='_') &&
        s.All(c=>char.IsLetterOrDigit(c)||c=='_');
    private static string Bv(long n)=>"(_ bv"+unchecked((ulong)n).ToString()+" 64)";
}
