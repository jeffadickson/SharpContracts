using System;
using System.Collections.Generic;
using System.Globalization;

namespace SharpContracts.Symbolic;

public enum SymbolicType { Boolean, Integer, Decimal, BitVector }
public enum SymbolicOperator { Add, Subtract, Equal, NotEqual, GreaterThan, GreaterOrEqual, LessThan, LessOrEqual, And, Or }
public abstract record ContractExpr(SymbolicType Type);
public sealed record ConstantExpr(object Value, SymbolicType ConstantType) : ContractExpr(ConstantType);
public sealed record ParameterExpr(string Name, SymbolicType ParameterType) : ContractExpr(ParameterType);
public sealed record BinaryExpr(SymbolicOperator Operator, ContractExpr Left, ContractExpr Right, SymbolicType ResultType) : ContractExpr(ResultType);
public sealed record NotExpr(ContractExpr Operand) : ContractExpr(SymbolicType.Boolean);

public static class Expr
{
    public static ContractExpr Bool(bool value) => new ConstantExpr(value, SymbolicType.Boolean);
    public static ContractExpr Int(long value) => new ConstantExpr(value, SymbolicType.Integer);
    public static ContractExpr Dec(decimal value) => new ConstantExpr(value, SymbolicType.Decimal);
    public static ContractExpr Param(string name, SymbolicType type)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Parameter name required", nameof(name));
        return new ParameterExpr(name, type);
    }
    public static ContractExpr Add(ContractExpr a, ContractExpr b) => Numeric(SymbolicOperator.Add,a,b);
    public static ContractExpr Subtract(ContractExpr a, ContractExpr b) => Numeric(SymbolicOperator.Subtract,a,b);
    public static ContractExpr Eq(ContractExpr a, ContractExpr b) => Comparison(SymbolicOperator.Equal,a,b,false);
    public static ContractExpr Ne(ContractExpr a, ContractExpr b) => Comparison(SymbolicOperator.NotEqual,a,b,false);
    public static ContractExpr Gt(ContractExpr a, ContractExpr b) => Comparison(SymbolicOperator.GreaterThan,a,b,true);
    public static ContractExpr Ge(ContractExpr a, ContractExpr b) => Comparison(SymbolicOperator.GreaterOrEqual,a,b,true);
    public static ContractExpr Lt(ContractExpr a, ContractExpr b) => Comparison(SymbolicOperator.LessThan,a,b,true);
    public static ContractExpr Le(ContractExpr a, ContractExpr b) => Comparison(SymbolicOperator.LessOrEqual,a,b,true);
    public static ContractExpr And(ContractExpr a, ContractExpr b) => Boolean(SymbolicOperator.And,a,b);
    public static ContractExpr Or(ContractExpr a, ContractExpr b) => Boolean(SymbolicOperator.Or,a,b);
    public static ContractExpr Not(ContractExpr x) { Need(x,SymbolicType.Boolean); return new NotExpr(x); }
    private static void Need(ContractExpr e, SymbolicType t) { if(e.Type!=t) throw new ArgumentException($"Expected {t}, got {e.Type}"); }
    private static ContractExpr Numeric(SymbolicOperator op,ContractExpr a,ContractExpr b)
    {
        Same(a,b); if(a.Type==SymbolicType.Boolean) throw new ArgumentException("Numeric operator expects numeric operands");
        return new BinaryExpr(op,a,b,a.Type);
    }
    private static ContractExpr Comparison(SymbolicOperator op,ContractExpr a,ContractExpr b,bool numeric)
    {
        Same(a,b); if(numeric && a.Type==SymbolicType.Boolean) throw new ArgumentException("Ordered comparison expects numeric operands");
        return new BinaryExpr(op,a,b,SymbolicType.Boolean);
    }
    private static ContractExpr Boolean(SymbolicOperator op,ContractExpr a,ContractExpr b)
    {
        Need(a,SymbolicType.Boolean);Need(b,SymbolicType.Boolean);
        return new BinaryExpr(op,a,b,SymbolicType.Boolean);
    }
    private static void Same(ContractExpr a,ContractExpr b) { if(a.Type!=b.Type) throw new ArgumentException("Operands must have matching types"); }
}

public static class ContractExprEvaluator
{
    public static bool Check(ContractExpr e,IReadOnlyDictionary<string,object> environment)
    {
        if(e.Type!=SymbolicType.Boolean) throw new ArgumentException("Condition must be Boolean");
        return (bool)Evaluate(e,environment);
    }
    public static object Evaluate(ContractExpr e,IReadOnlyDictionary<string,object> environment) => e switch
    {
        ConstantExpr c => Validate(c.Value,c.Type),
        ParameterExpr p => environment.TryGetValue(p.Name,out var value)
            ? Validate(value,p.Type) : throw new KeyNotFoundException("Missing parameter: "+p.Name),
        NotExpr n => !(bool)Evaluate(n.Operand,environment),
        BinaryExpr b => EvalBinary(b,environment),
        _ => throw new NotSupportedException("Unknown expression node")
    };
    private static object Validate(object value,SymbolicType type) => type switch
    {
        SymbolicType.Boolean when value is bool => value,
        SymbolicType.Integer when value is long => value,
        SymbolicType.Decimal when value is decimal => value,
        _ => throw new ArgumentException($"Value does not match {type}")
    };
    private static object EvalBinary(BinaryExpr b,IReadOnlyDictionary<string,object> env)
    {
        // Short-circuit semantics are explicit so rejected branches need not resolve missing parameters.
        if(b.Operator==SymbolicOperator.And)
        {
            bool left=(bool)Evaluate(b.Left,env);
            return left && (bool)Evaluate(b.Right,env);
        }
        if(b.Operator==SymbolicOperator.Or)
        {
            bool left=(bool)Evaluate(b.Left,env);
            return left || (bool)Evaluate(b.Right,env);
        }
        object a=Evaluate(b.Left,env),c=Evaluate(b.Right,env);
        int cmp=b.Left.Type switch
        {
            SymbolicType.Integer => ((long)a).CompareTo((long)c),
            SymbolicType.Decimal => ((decimal)a).CompareTo((decimal)c),
            SymbolicType.Boolean => ((bool)a).CompareTo((bool)c),
            _ => throw new NotSupportedException()
        };
        return b.Operator switch
        {
            SymbolicOperator.Equal => cmp==0,
            SymbolicOperator.NotEqual => cmp!=0,
            SymbolicOperator.GreaterThan => cmp>0,
            SymbolicOperator.GreaterOrEqual => cmp>=0,
            SymbolicOperator.LessThan => cmp<0,
            SymbolicOperator.LessOrEqual => cmp<=0,
            SymbolicOperator.Add when b.Type==SymbolicType.Integer => checked((long)a+(long)c),
            SymbolicOperator.Subtract when b.Type==SymbolicType.Integer => checked((long)a-(long)c),
            SymbolicOperator.Add when b.Type==SymbolicType.Decimal => checked((decimal)a+(decimal)c),
            SymbolicOperator.Subtract when b.Type==SymbolicType.Decimal => checked((decimal)a-(decimal)c),
            _ => throw new NotSupportedException()
        };
    }
}

public sealed record SymbolicContract(string Id, ContractKind Kind, string Description, ContractExpr Predicate)
{
    public void Check(IReadOnlyDictionary<string,object> environment)
    {
        if(!ContractExprEvaluator.Check(Predicate,environment))
            throw new ContractViolationException(Kind,Id,Description);
    }
}
