using SharpContracts;
using Xunit;

namespace SharpContracts.Tests;

public interface ITransferService
{
    void Send([ContractPositive("AUTO-001")] decimal amount, [ContractNotNull("AUTO-002")] string recipient);
}
public sealed class TransferService : ITransferService
{
    public int Calls { get; private set; }
    public void Send(decimal amount, string recipient) => Calls++;
}
public sealed class ContractTests
{
    [Fact] public void ProxyChecksAttributeContractsBeforeCall()
    {
        var target = new TransferService();
        var proxy = ContractProxy.Create<ITransferService>(target);
        var violation = Assert.Throws<ContractViolationException>(() => proxy.Send(0m, "Alice"));
        Assert.Equal("AUTO-001", violation.ContractId);
        Assert.Equal(0, target.Calls);
        violation = Assert.Throws<ContractViolationException>(() => proxy.Send(1m, null!));
        Assert.Equal("AUTO-002", violation.ContractId);
        Assert.Equal(0, target.Calls);
        proxy.Send(1m, "Alice");
        Assert.Equal(1, target.Calls);
    }
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
