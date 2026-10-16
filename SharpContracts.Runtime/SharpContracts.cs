using System;

namespace SharpContracts;

public enum ContractKind { Precondition, Postcondition, Invariant }

public sealed class ContractViolationException : Exception
{
    public ContractKind Kind { get; }
    public string ContractId { get; }
    public ContractViolationException(ContractKind kind, string id, string message)
        : base($"{kind} [{id}]: {message}") { Kind = kind; ContractId = id; }
}

public static class Contract
{
    public static void Require(bool condition, string id, string message) => Check(condition, ContractKind.Precondition, id, message);
    public static void Ensure(bool condition, string id, string message) => Check(condition, ContractKind.Postcondition, id, message);
    /// <summary>Checks an intermediate property at the current execution point.</summary>
    public static void Assert(bool condition, string id, string message) => Check(condition, ContractKind.Postcondition, id, message);
    public static void Invariant(bool condition, string id, string message) => Check(condition, ContractKind.Invariant, id, message);
    public static T Old<T>(T value) => value; // Value copy, not a deep snapshot of reference objects.
    private static void Check(bool condition, ContractKind kind, string id, string message)
    {
        if (!condition) throw new ContractViolationException(kind, id, message);
    }
}

public sealed class Account
{
    public string Name { get; }
    public decimal Balance { get; private set; }
    public Account(string name, decimal openingBalance)
    {
        Contract.Require(!string.IsNullOrWhiteSpace(name), "ACC-001", "name required");
        Contract.Require(openingBalance >= 0, "ACC-002", "opening balance must be nonnegative");
        Name = name;
        Balance = openingBalance;
        CheckInvariant();
    }
    public void CheckInvariant() => Contract.Invariant(Balance >= 0, "INV-001", $"{Name} balance cannot be negative");
    internal void Debit(decimal amount) => Balance -= amount;
    internal void Credit(decimal amount) => Balance += amount;
}

public static class Banking
{
    public static void Transfer(Account from, Account to, decimal amount, bool injectBug = false)
    {
        Contract.Require(from is not null, "PRE-001", "source is required");
        Contract.Require(to is not null, "PRE-002", "destination is required");
        Contract.Require(!ReferenceEquals(from, to), "PRE-003", "accounts must differ");
        Contract.Require(amount > 0, "PRE-004", "amount must be positive");
        Contract.Require(from!.Balance >= amount, "PRE-005", "insufficient funds");
        from.CheckInvariant(); to!.CheckInvariant();
        decimal sourceBefore = Contract.Old(from.Balance);
        decimal destinationBefore = Contract.Old(to.Balance);
        decimal totalBefore = sourceBefore + destinationBefore;
        from.Debit(amount);
        to.Credit(injectBug ? amount + 1m : amount);
        Contract.Ensure(from.Balance == sourceBefore - amount, "POST-001", "source balance must decrease by amount");
        Contract.Ensure(to.Balance == destinationBefore + amount, "POST-002", "destination balance must increase by amount");
        Contract.Ensure(from.Balance + to.Balance == totalBefore, "POST-003", "funds must be conserved");
        from.CheckInvariant(); to.CheckInvariant();
    }
}
