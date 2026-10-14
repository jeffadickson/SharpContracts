using SharpContracts;
using Xunit;

namespace SharpContracts.Tests;

public sealed class DefinitionTests
{
    [Fact]
    public void BoundaryChecksPreconditionBeforeInvokingAction()
    {
        bool invoked = false;
        var pre = new ContractSet<int>().Add(new("PRE-X", ContractKind.Precondition, "positive", x => x > 0));
        var post = new ContractSet<(int Input, int Result)>();
        Assert.Throws<ContractViolationException>(() =>
            ContractBoundary.Invoke(-1, x => { invoked = true; return x; }, pre, post));
        Assert.False(invoked);
    }

    [Fact]
    public void BoundaryChecksResult()
    {
        var pre = new ContractSet<int>();
        var post = new ContractSet<(int Input, int Result)>()
            .Add(new("POST-X", ContractKind.Postcondition, "square", p => p.Result == p.Input * p.Input));
        var result = ContractBoundary.Invoke(3, x => x*x, pre, post);
        Assert.Equal(9, result);
        var failure = Assert.Throws<ContractViolationException>(() =>
            ContractBoundary.Invoke(3, x => x + 1, pre, post));
        Assert.Equal("POST-X", failure.ContractId);
    }

    [Fact]
    public void DuplicateIdentifiersAreRejected()
    {
        var set = new ContractSet<int>().Add(new("A", ContractKind.Precondition, "one", _ => true));
        Assert.Throws<ArgumentException>(() => set.Add(new("A", ContractKind.Precondition, "two", _ => true)));
    }
}
