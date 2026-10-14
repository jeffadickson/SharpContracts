using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpContracts.Symbolic;

/// <summary>Emits SMT-LIB QF_LIA/QF_LRA style assertions. No solver execution or proof claim.</summary>
public static class SmtLibPrinter
{
    public static string Print(ContractExpr predicate)
    {
        if(predicate.Type!=SymbolicType.Boolean) throw new ArgumentException("Predicate must be Boolean");
        var vars=new SortedDictionary<string,SymbolicType>(StringComparer.Ordinal);
        Collect(predicate,vars);
        var b=new StringBuilder();
        b.AppendLine("; SharpContracts symbolic predicate — solver input, not a proof");
        b.AppendLine("(set-logic ALL)");
        foreach(var v in vars)
            b.AppendLine($"(declare-const {Symbol(v.Key)} {Sort(v.Value)})");
        b.AppendLine($"(assert {Render(predicate)})");
        b.AppendLine("(check-sat)");
        return b.ToString();
    }
    private static string Sort(SymbolicType t)=>t switch { SymbolicType.Boolean=>"Bool",SymbolicType.Integer=>"Int",SymbolicType.Decimal=>"Real",_=>throw new NotSupportedException()};
    private static string Symbol(string name) => "|"+name.Replace("\\","\\\\").Replace("|","\\|")+"|";
    private static void Collect(ContractExpr e,SortedDictionary<string,SymbolicType> vars)
    {
        switch(e)
        {
            case ParameterExpr p:
                if(vars.TryGetValue(p.Name,out var old)&&old!=p.Type) throw new ArgumentException("Conflicting parameter types: "+p.Name);
                vars[p.Name]=p.Type;break;
            case BinaryExpr b:Collect(b.Left,vars);Collect(b.Right,vars);break;
            case NotExpr n:Collect(n.Operand,vars);break;
        }
    }
    private static string Render(ContractExpr e)=>e switch
    {
        ConstantExpr {Value:bool b}=>b?"true":"false",
        ConstantExpr {Value:long n} when n<0=>$"(- {-(System.Numerics.BigInteger)n})",
        ConstantExpr {Value:long n}=>n.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ConstantExpr {Value:decimal d}=>Decimal(d),
        ParameterExpr p=>Symbol(p.Name),
        NotExpr n=>$"(not {Render(n.Operand)})",
        BinaryExpr b=>$"({Op(b.Operator)} {Render(b.Left)} {Render(b.Right)})",
        _=>throw new NotSupportedException()
    };
    private static string Decimal(decimal d)
    {
        string s=d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if(s.StartsWith("-",StringComparison.Ordinal)) return "(- "+Decimal(-d)+")";
        return s.Contains('.')?s:s+".0";
    }
    private static string Op(SymbolicOperator op)=>op switch
    {
        SymbolicOperator.Add=>"+", SymbolicOperator.Subtract=>"-",
        SymbolicOperator.Equal=>"=", SymbolicOperator.NotEqual=>"distinct",
        SymbolicOperator.GreaterThan=>">",SymbolicOperator.GreaterOrEqual=>">=",
        SymbolicOperator.LessThan=>"<", SymbolicOperator.LessOrEqual=>"<=",
        SymbolicOperator.And=>"and",SymbolicOperator.Or=>"or",_=>throw new NotSupportedException()
    };
}
