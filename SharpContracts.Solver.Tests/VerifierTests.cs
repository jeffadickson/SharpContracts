using SharpContracts.Symbolic;
using SharpContracts.Solver;
using Xunit;

namespace SharpContracts.Solver.Tests;
public sealed class VerifierTests
{
    [Fact] public void ProvesGreaterThanZeroFromStrongerAssumption()
    {
        var x=Expr.Param("x",SymbolicType.Integer);
        var result=Z3Verifier.Verify(new[]{Expr.Gt(x,Expr.Int(5))},Expr.Gt(x,Expr.Int(0)));
        Assert.Equal(VerificationStatus.Proved,result.Status);
    }

    [Fact] public void ReturnsCounterexampleWhenImplicationIsFalse()
    {
        var x=Expr.Param("x",SymbolicType.Integer);
        var result=Z3Verifier.Verify(new[]{Expr.Ge(x,Expr.Int(0))},Expr.Gt(x,Expr.Int(0)));
        Assert.Equal(VerificationStatus.Counterexample,result.Status);
        Assert.Contains("x",result.Model);
    }

    [Fact] public void ProvesTransferPreservesTotalUnderIdealIntegers()
    {
        var a=Expr.Param("a",SymbolicType.Integer);
        var b=Expr.Param("b",SymbolicType.Integer);
        var amount=Expr.Param("amount",SymbolicType.Integer);
        var updatedA=Expr.Subtract(a,amount);
        var updatedB=Expr.Add(b,amount);
        var balance=Expr.Eq(Expr.Add(updatedA,updatedB),Expr.Add(a,b));
        var result=Z3Verifier.Verify(new[]{Expr.Ge(amount,Expr.Int(0))},balance);
        Assert.Equal(VerificationStatus.Proved,result.Status);
    }
}
