using System;
using System.Collections.Generic;
using SharpContracts;
using SharpContracts.Symbolic;
using Xunit;

namespace SharpContracts.Tests;

public sealed class SymbolicTests
{
    [Fact]
    public void SameExpressionChecksAndPrints()
    {
        var amount=Expr.Param("amount",SymbolicType.Decimal);
        var positive=Expr.Gt(amount,Expr.Dec(0m));
        var env=new Dictionary<string,object>{{"amount",25m}};
        Assert.True(ContractExprEvaluator.Check(positive,env));
        Assert.False(ContractExprEvaluator.Check(positive,new Dictionary<string,object>{{"amount",0m}}));
        string script=SmtLibPrinter.Print(positive);
        Assert.Contains("(declare-const |amount| Real)",script);
        Assert.Contains("(assert (> |amount| 0.0))",script);
    }
    [Fact]
    public void TypeErrorsRejectedDuringConstruction()
    {
        Assert.Throws<ArgumentException>(()=>Expr.Add(Expr.Int(1),Expr.Dec(1m)));
        Assert.Throws<ArgumentException>(()=>Expr.Gt(Expr.Bool(true),Expr.Bool(false)));
    }
    [Fact]
    public void ShortCircuitSkipsMissingVariable()
    {
        var expr=Expr.And(Expr.Bool(false),Expr.Gt(Expr.Param("missing",SymbolicType.Integer),Expr.Int(0)));
        Assert.False(ContractExprEvaluator.Check(expr,new Dictionary<string,object>()));
    }
    [Fact]
    public void FailedSymbolicContractHasIdentifier()
    {
        var rule=new SymbolicContract("SC-DEMO",ContractKind.Precondition,"must be positive",
            Expr.Gt(Expr.Param("n",SymbolicType.Integer),Expr.Int(0)));
        var error=Assert.Throws<ContractViolationException>(()=>
            rule.Check(new Dictionary<string,object>{{"n",0L}}));
        Assert.Equal("SC-DEMO",error.ContractId);
    }
}
