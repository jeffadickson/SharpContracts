using Microsoft.CodeAnalysis;

namespace SharpContracts.Verification;

/// <summary>Resolve the trusted runtime assembly, never just a source-defined lookalike name.</summary>
internal static class RuntimeSymbolPolicy
{
    internal static bool IsRuntimeType(INamedTypeSymbol? type, string metadataName)
        => type is not null && type.ToDisplayString() == metadataName &&
           type.Locations.All(location => location.IsInMetadata) &&
           type.ContainingAssembly.Identity.Name == typeof(Contract).Assembly.GetName().Name;
}
