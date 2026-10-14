using System;
using System.Collections.Generic;

namespace SharpContracts;

/// <summary>Versioned, strongly typed runtime contract model. Deliberately no expression serialization yet.</summary>
public sealed record ContractDefinition<T>(
    string Id, ContractKind Kind, string Description, Func<T, bool> Predicate)
{
    public void Check(T context)
    {
        if (string.IsNullOrWhiteSpace(Id)) throw new ArgumentException("Contract identifier is required", nameof(Id));
        if (!Predicate(context)) throw new ContractViolationException(Kind, Id, Description);
    }
}

public sealed class ContractSet<T>
{
    private readonly List<ContractDefinition<T>> _definitions = new();
    public IReadOnlyList<ContractDefinition<T>> Definitions => _definitions.AsReadOnly();
    public ContractSet<T> Add(ContractDefinition<T> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.Id)) throw new ArgumentException("Contract ID required");
        if (_definitions.Exists(x => x.Id == definition.Id)) throw new ArgumentException("Duplicate contract ID: " + definition.Id);
        _definitions.Add(definition);
        return this;
    }
    public void Check(T context)
    {
        foreach (var definition in _definitions) definition.Check(context);
    }
}

/// <summary>Explicit boundary wrapper: requires before action, ensures on normal return.</summary>
public static class ContractBoundary
{
    public static TResult Invoke<TInput, TResult>(
        TInput input, Func<TInput, TResult> action,
        ContractSet<TInput> preconditions,
        ContractSet<(TInput Input, TResult Result)> postconditions)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(preconditions);
        ArgumentNullException.ThrowIfNull(postconditions);
        preconditions.Check(input);
        var result = action(input);
        postconditions.Check((input, result));
        return result;
    }
}
