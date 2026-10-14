using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Z3;
using SharpContracts.Symbolic;
using Z3Expr = Microsoft.Z3.Expr;

namespace SharpContracts.Solver;

public enum VerificationStatus { Proved, Counterexample, Unknown }
public sealed record VerificationResult(VerificationStatus Status, string? Model, string? Reason);

/// <summary>Prove assumptions imply a claim by checking assumptions AND NOT claim for satisfiability.</summary>
public static class Z3Verifier
{
    public static VerificationResult Verify(
        IReadOnlyList<ContractExpr> assumptions, ContractExpr claim, uint timeoutMilliseconds = 5000)
    {
        if (claim.Type != SymbolicType.Boolean) throw new ArgumentException("Claim must be Boolean");
        using var context = new Context();
        using var solver = context.MkSolver();
        using var parameters = context.MkParams();
        parameters.Add("timeout", timeoutMilliseconds);
        solver.Parameters = parameters;
        var variables = new Dictionary<string, Z3Expr>(StringComparer.Ordinal);
        var types = new Dictionary<string, SymbolicType>(StringComparer.Ordinal);
        foreach (var assumption in assumptions)
        {
            if (assumption.Type != SymbolicType.Boolean) throw new ArgumentException("Assumptions must be Boolean");
            solver.Assert((BoolExpr)Convert(assumption, context, variables, types));
        }
        solver.Assert(context.MkNot((BoolExpr)Convert(claim, context, variables, types)));
        return solver.Check() switch
        {
            Status.UNSATISFIABLE => new(VerificationStatus.Proved, null, null),
            Status.SATISFIABLE => new(VerificationStatus.Counterexample, solver.Model.ToString(), null),
            _ => new(VerificationStatus.Unknown, null, solver.ReasonUnknown)
        };
    }

    private static Z3Expr Convert(ContractExpr e, Context c,
        Dictionary<string,Z3Expr> vars, Dictionary<string,SymbolicType> types)
    {
        switch (e)
        {
            case ConstantExpr { Value: bool b }: return c.MkBool(b);
            case ConstantExpr { Value: long n }: return c.MkInt(n.ToString(CultureInfo.InvariantCulture));
            case ConstantExpr { Value: decimal d }: return c.MkReal(d.ToString(CultureInfo.InvariantCulture));
            case ParameterExpr p:
                if(types.TryGetValue(p.Name, out var existing) && existing != p.Type)
                    throw new ArgumentException("Conflicting type for " + p.Name);
                types[p.Name] = p.Type;
                if(!vars.TryGetValue(p.Name,out var variable))
                {
                    variable = p.Type switch
                    {
                        SymbolicType.Boolean => c.MkBoolConst(p.Name),
                        SymbolicType.Integer => c.MkIntConst(p.Name),
                        SymbolicType.Decimal => c.MkRealConst(p.Name),
                        _ => throw new NotSupportedException()
                    };
                    vars[p.Name]=variable;
                }
                return variable;
            case NotExpr n: return c.MkNot((BoolExpr)Convert(n.Operand,c,vars,types));
            case BinaryExpr b:
                var l=Convert(b.Left,c,vars,types);
                var r=Convert(b.Right,c,vars,types);
                return b.Operator switch
                {
                    SymbolicOperator.And => c.MkAnd((BoolExpr)l,(BoolExpr)r),
                    SymbolicOperator.Or => c.MkOr((BoolExpr)l,(BoolExpr)r),
                    SymbolicOperator.Equal => c.MkEq(l,r),
                    SymbolicOperator.NotEqual => c.MkNot(c.MkEq(l,r)),
                    SymbolicOperator.Add => c.MkAdd((ArithExpr)l,(ArithExpr)r),
                    SymbolicOperator.Subtract => c.MkSub((ArithExpr)l,(ArithExpr)r),
                    SymbolicOperator.GreaterThan => c.MkGt((ArithExpr)l,(ArithExpr)r),
                    SymbolicOperator.GreaterOrEqual => c.MkGe((ArithExpr)l,(ArithExpr)r),
                    SymbolicOperator.LessThan => c.MkLt((ArithExpr)l,(ArithExpr)r),
                    SymbolicOperator.LessOrEqual => c.MkLe((ArithExpr)l,(ArithExpr)r),
                    _ => throw new NotSupportedException()
                };
            default: throw new NotSupportedException("Unsupported symbolic node: "+e.GetType().Name);
        }
    }
}
