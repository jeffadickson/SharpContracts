using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace SharpContracts.Verification;

/// <summary>Reports the compiler-resolved numeric types and operators; never claims proof.</summary>
public sealed record BoundNumericOperation(
    int Line, string Expression, string ResultType, NumericModelKind Kind,
    int? Width, string Operator, bool IsUserDefined, bool IsChecked);

public sealed record NumericBindingReport(
    IReadOnlyList<BoundNumericOperation> Operations, IReadOnlyList<string> Errors)
{
    public bool IsSupported => Errors.Count == 0;
}

/// <summary>
/// Roslyn semantic preflight for numeric operations, including promotions and custom overloads.
/// This is a verification gate, not an SMT backend.
/// </summary>
public static class NumericSemanticInspector
{
    public static NumericBindingReport Inspect(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var references = new[] {
            typeof(object).Assembly.Location,
            typeof(Console).Assembly.Location,
            typeof(System.Runtime.CompilerServices.IsExternalInit).Assembly.Location
        }.Distinct().Select(path => MetadataReference.CreateFromFile(path)).ToArray();

        var compilation = CSharpCompilation.Create("SharpContracts.NumericPreflight",
            new[] { tree }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var model = compilation.GetSemanticModel(tree);
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString()).ToList();
        var operations = new List<BoundNumericOperation>();

        foreach (var syntax in tree.GetRoot().DescendantNodes().OfType<BinaryExpressionSyntax>())
        {
            if (model.GetOperation(syntax) is not IBinaryOperation operation) continue;
            if (operation.Type is null)
            {
                errors.Add("Unresolved operator type: " + syntax);
                continue;
            }
            var descriptor = NumericTypeDescriptor.FromSymbol(operation.Type);
            var isCustom = operation.OperatorMethod is not null &&
                           !NumericOperatorPolicy.CanLowerBuiltinArithmetic(operation.OperatorMethod);
            operations.Add(new(
                tree.GetLineSpan(syntax.Span).StartLinePosition.Line + 1,
                syntax.ToString(), descriptor.DisplayName, descriptor.Kind,
                descriptor.Width, operation.OperatorKind.ToString(), isCustom,
                operation.IsChecked));
            if (isCustom)
                errors.Add("Unverified custom operator at line " +
                           (tree.GetLineSpan(syntax.Span).StartLinePosition.Line + 1) +
                           ": " + operation.OperatorMethod!.ToDisplayString());
            if (descriptor.Kind is NumericModelKind.FloatingPoint or NumericModelKind.Decimal
                or NumericModelKind.Unsupported or NumericModelKind.Custom)
                errors.Add("Numeric model not implemented: " + descriptor.DisplayName +
                           " at line " + (tree.GetLineSpan(syntax.Span).StartLinePosition.Line + 1));
        }
        return new(operations, errors);
    }
}
