using System;
using SharpContracts;

static void Case(string name, Action action, ContractKind? expected = null)
{
    try { action(); if (expected is not null) throw new Exception($"FAIL {name}: expected {expected}"); Console.WriteLine($"PASS {name}"); }
    catch (ContractViolationException e) when (expected == e.Kind) { Console.WriteLine($"PASS {name}: detected {e.ContractId}"); }
}
Case("valid transfer", () => Banking.Transfer(new Account("Checking", 100m), new Account("Savings", 50m), 25m));
Case("negative amount", () => Banking.Transfer(new Account("Checking", 100m), new Account("Savings", 50m), -1m), ContractKind.Precondition);
Case("insufficient funds", () => Banking.Transfer(new Account("Checking", 100m), new Account("Savings", 50m), 125m), ContractKind.Precondition);
Case("injected conservation bug", () => Banking.Transfer(new Account("Checking", 100m), new Account("Savings", 50m), 25m, true), ContractKind.Postcondition);
