using System;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace SharpContracts;

/// <summary>A precondition on a parameter of an interface method.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class ContractNotNullAttribute : Attribute
{
    public string Id { get; }
    public ContractNotNullAttribute(string id) => Id = id;
}

/// <summary>Requires a positive decimal, int, long, double or float argument.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class ContractPositiveAttribute : Attribute
{
    public string Id { get; }
    public ContractPositiveAttribute(string id) => Id = id;
}

public static class ContractProxy
{
    /// <summary>
    /// Wrap an interface implementation; checks annotated interface parameters prior to invocation.
    /// Does not intercept self-calls or methods invoked directly on the target.
    /// </summary>
    public static T Create<T>(T target) where T : class
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!typeof(T).IsInterface) throw new ArgumentException("T must be an interface");
        var proxy = DispatchProxy.Create<T, ValidationProxy<T>>();
        ((ValidationProxy<T>)(object)proxy).Target = target;
        return proxy;
    }
}

/// <summary>For DispatchProxy infrastructure; do not instantiate directly.</summary>
public class ValidationProxy<T> : DispatchProxy where T : class
{
    public T Target { get; set; } = default!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null) throw new InvalidOperationException("Missing target method");
        args ??= Array.Empty<object?>();
        var parameters = targetMethod.GetParameters();
        for (int i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            var nonNull = p.GetCustomAttribute<ContractNotNullAttribute>();
            if (nonNull is not null)
                Contract.Require(args[i] is not null, nonNull.Id, p.Name + " must not be null");
            var positive = p.GetCustomAttribute<ContractPositiveAttribute>();
            if (positive is not null)
            {
                var v = args[i];
                bool valid = v switch
                {
                    decimal d => d > 0m,
                    int n => n > 0,
                    long n => n > 0,
                    double d => d > 0.0,
                    float f => f > 0f,
                    _ => false
                };
                Contract.Require(valid, positive.Id, p.Name + " must be positive");
            }
        }
        try { return targetMethod.Invoke(Target, args); }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
    }
}
