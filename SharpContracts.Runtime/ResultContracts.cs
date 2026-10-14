using System;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace SharpContracts;

/// <summary>Checks that a numeric synchronous return value is nonnegative.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ContractNonNegativeResultAttribute : Attribute
{
    public string Id { get; }
    public ContractNonNegativeResultAttribute(string id) => Id = id;
}

public static class ContractResultEvaluator
{
    public static void Check(MethodInfo method, object? result)
    {
        var rule = method.GetCustomAttribute<ContractNonNegativeResultAttribute>();
        if (rule is null) return;
        bool valid = result switch
        {
            decimal d => d >= 0m,
            int n => n >= 0,
            long n => n >= 0,
            double d => !double.IsNaN(d) && d >= 0,
            float f => !float.IsNaN(f) && f >= 0,
            _ => false
        };
        Contract.Ensure(valid, rule.Id, "Method result must be nonnegative");
    }
}
