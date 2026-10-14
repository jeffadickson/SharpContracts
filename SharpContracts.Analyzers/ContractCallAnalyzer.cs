using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SharpContracts.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ContractCallAnalyzer : DiagnosticAnalyzer
{
    public const string EmptyId = "SC1001";
    public const string AlwaysFalse = "SC1002";
    public const string ImpureCondition = "SC1003";

    private static readonly DiagnosticDescriptor IdRule = new(
        EmptyId, "Contract identifier required", "Contract check must specify a nonempty identifier",
        "SharpContracts", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor FalseRule = new(
        AlwaysFalse, "Contract always fails", "Contract '{0}' uses an always-false condition",
        "SharpContracts", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ImpureRule = new(
        ImpureCondition, "Contract condition may mutate state",
        "Contract '{0}' contains an assignment or increment/decrement; contract conditions should be pure",
        "SharpContracts", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(IdRule, FalseRule, ImpureRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var call = (InvocationExpressionSyntax)context.Node;
        var symbol = context.SemanticModel.GetSymbolInfo(call).Symbol as IMethodSymbol;
        if (symbol is null || symbol.ContainingType.ToDisplayString() != "SharpContracts.Contract")
            return;
        if (symbol.Name != "Require" && symbol.Name != "Ensure" && symbol.Name != "Invariant")
            return;
        var args = call.ArgumentList.Arguments;
        if (args.Count < 3) return;
        var idArg = FindArgument(args, "id", 1);
        var conditionArg = FindArgument(args, "condition", 0);
        if (idArg is null || conditionArg is null) return;

        var constantId = context.SemanticModel.GetConstantValue(idArg.Expression);
        if (constantId.HasValue && constantId.Value is string id && string.IsNullOrWhiteSpace(id))
            context.ReportDiagnostic(Diagnostic.Create(IdRule, idArg.GetLocation()));

        string name = constantId.HasValue && constantId.Value is string value ? value : "(dynamic)";
        var condition = context.SemanticModel.GetConstantValue(conditionArg.Expression);
        if (condition.HasValue && condition.Value is bool b && !b)
            context.ReportDiagnostic(Diagnostic.Create(FalseRule, conditionArg.GetLocation(), name));

        foreach (var node in conditionArg.Expression.DescendantNodesAndSelf())
        {
            if (node is AssignmentExpressionSyntax ||
                node.IsKind(SyntaxKind.PreIncrementExpression) ||
                node.IsKind(SyntaxKind.PostIncrementExpression) ||
                node.IsKind(SyntaxKind.PreDecrementExpression) ||
                node.IsKind(SyntaxKind.PostDecrementExpression))
            {
                context.ReportDiagnostic(Diagnostic.Create(ImpureRule, conditionArg.GetLocation(), name));
                break;
            }
        }
    }

    private static ArgumentSyntax? FindArgument(SeparatedSyntaxList<ArgumentSyntax> args, string name, int positional)
    {
        foreach (var arg in args)
            if (arg.NameColon?.Name.Identifier.ValueText == name) return arg;
        if (positional >= args.Count) return null;
        return args[positional].NameColon is null ? args[positional] : null;
    }
}
