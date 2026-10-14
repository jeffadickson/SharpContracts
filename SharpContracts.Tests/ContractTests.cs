using SharpContracts;
using Xunit;

namespace SharpContracts.Tests;

public sealed class ContractTests
{
    [Fact] public void ValidTransferPreservesTotal()
    {
        var a = new Account("A", 100m);
        var b = new Account("B", 50m);
        Banking.Transfer(a, b, 25m);
        Assert.Equal(75m, a.Balance);
        Assert.Equal(75m, b.Balance);
        Assert.Equal(150m, a.Balance + b.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void InvalidTransferIsRejectedBeforeMutation(decimal amount)
    {
        var a = new Account("A", 100m);
        var b = new Account("B", 50m);
        var error = Assert.Throws<ContractViolationException>(() => Banking.Transfer(a, b, amount));
        Assert.Equal(ContractKind.Precondition, error.Kind);
        Assert.Equal(100m, a.Balance);
        Assert.Equal(50m, b.Balance);
    }

    [Fact] public void FaultInjectionTriggersPostcondition()
    {
        var a = new Account("A", 100m);
        var b = new Account("B", 50m);
        var error = Assert.Throws<ContractViolationException>(() => Banking.Transfer(a, b, 25m, true));
        Assert.Equal("POST-002", error.ContractId);
        // A postcondition is checked after mutation: this API is NOT transactional.
        Assert.Equal(76m, b.Balance);
    }

    [Fact] public void InvalidOpeningBalanceIsRejected()
    {
        var error = Assert.Throws<ContractViolationException>(() => new Account("A", -1m));
        Assert.Equal("ACC-002", error.ContractId);
    }
}
