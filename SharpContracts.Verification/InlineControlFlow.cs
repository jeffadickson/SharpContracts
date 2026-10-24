using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Z3;
using SharpContracts.Solver;
using SharpContracts.Symbolic;

namespace SharpContracts.Verification;

/// <summary>
/// SC-091/092: compile the whole supported statement tree before executing bounded symbolic paths.
/// Immutable expression snapshots isolate siblings; contracts still apply at their source call sites.
/// </summary>
internal sealed class InlineControlFlow
{
    private const int MaximumGeneratedPaths=256;
    private const int MaximumStatements=1024;
    private const int MaximumStatementDepth=64;
    private const int MaximumExpressionSize=4096;
    private const int MaximumExpressionDepth=128;
    private static readonly ClrScalarType UInt32=ClrScalarType.Integral(32,false);
    private readonly Context context;
    private readonly Microsoft.Z3.Solver solver;
    private readonly ClrExpressionEncoder encoder;
    private readonly Dictionary<ISymbol,ClrExpression> symbols=new(SymbolEqualityComparer.Default);
    private readonly Dictionary<string,Value> templateValues=new(StringComparer.Ordinal);
    private readonly Dictionary<string,Value> entryValues=new(StringComparer.Ordinal);
    private readonly Dictionary<ISymbol,ClrExpression> inputs=new(SymbolEqualityComparer.Default);
    private readonly List<Goal> goals=new();
    private readonly Dictionary<int,List<MethodCheck>> results=new();
    private readonly ClrOperationLowerer lowerer;
    private int localIndex,statementCount,generatedPaths=1;

    private abstract record Statement;
    private sealed record Block(Statement[] Children):Statement;
    private sealed record Store(string Name,ClrExpression Expression):Statement;
    private sealed record Branch(ClrExpression Condition,Statement Yes,Statement? No):Statement;
    private sealed record Exit(ClrExpression Expression):Statement;
    private sealed record Goal(int Site,string Label,int Line,ClrExpression Predicate):Statement;
    // Size/depth count the expanded tree, not only distinct DAG nodes. This bounds the work
    // of the existing recursive encoder even for repeated updates such as y = y + y.
    private sealed record Value(ClrExpression Expression,int Size=1,int Depth=1);
    private sealed class Path
    {
        internal Dictionary<string,Value> Values { get; }
        internal List<BoolExpr> Conditions { get; }
        internal Path(Dictionary<string,Value> values,List<BoolExpr>? conditions=null)
            => (Values,Conditions)=(values,conditions ?? new());
        internal Path Fork(BoolExpr condition)
            => new(new(Values,StringComparer.Ordinal),new(Conditions){condition});
    }

    internal InlineControlFlow(Context context,Microsoft.Z3.Solver solver,IMethodSymbol method)
    {
        this.context=context;
        this.solver=solver;
        encoder=new(context);
        foreach (var parameter in method.Parameters)
        {
            var expression=new ClrParameter(parameter.Name,UInt32);
            symbols.Add(parameter,expression);
            inputs.Add(parameter,expression);
            templateValues.Add(parameter.Name,new(expression));
            entryValues.Add(parameter.Name,new(expression));
        }
        lowerer=new(symbols);
    }

    internal IReadOnlyList<MethodCheck> Verify(IBlockOperation body)
    {
        var requirements=new List<ClrExpression>();
        var statements=new List<Statement>();
        bool inEntryPrefix=true,terminated=false;
        foreach (var operation in body.Operations)
        {
            var call=ContractCall(operation);
            if (inEntryPrefix && call?.TargetMethod.Name=="Require")
            {
                CountStatement(0);
                requirements.Add(ContractPredicate(call).Predicate);
                continue;
            }
            inEntryPrefix=false;
            if (terminated) throw new NotSupportedException("Statements after unconditional return unsupported");
            var statement=Compile(operation,0);
            statements.Add(statement);
            terminated=AlwaysReturns(statement);
        }
        // Translation above visits both arms, including statically dead code. No solver
        // result can hide an unsupported operation in an unexecuted branch or suffix.
        if (goals.Count==0) throw new NotSupportedException("At least one Ensure or Assert required");
        foreach (var requirement in requirements) solver.Assert((BoolExpr)encoder.Encode(requirement));
        var feasibility=solver.Check();
        if (feasibility==Status.UNSATISFIABLE)
            throw new NotSupportedException("Inconsistent preconditions: no permitted inputs");
        if (feasibility==Status.UNKNOWN)
            throw new NotSupportedException("Precondition feasibility unknown: "+solver.ReasonUnknown);
        var unfinished=Execute(statements,new(){new(new(entryValues,StringComparer.Ordinal))});
        if (unfinished.Count!=0) throw new NotSupportedException("Every normal path must return");
        return goals.Select(Aggregate).ToArray();
    }

    private void CountStatement(int depth)
    {
        if (++statementCount>MaximumStatements || depth>MaximumStatementDepth)
            throw new NotSupportedException("Statement count or nesting budget exceeded");
    }

    private Statement Compile(IOperation operation,int depth)
    {
        CountStatement(depth);
        if (operation is IBlockOperation block)
        {
            var children=new List<Statement>();
            bool terminated=false;
            foreach (var child in block.Operations)
            {
                if (terminated) throw new NotSupportedException("Statements after unconditional return unsupported");
                var compiled=Compile(child,depth+1);
                children.Add(compiled);
                terminated=AlwaysReturns(compiled);
            }
            return new Block(children.ToArray());
        }
        if (operation is IConditionalOperation {Type:null} branch)
            return new Branch(Lower(branch.Condition,ClrScalarType.Boolean),Compile(branch.WhenTrue,depth+1),
                branch.WhenFalse is null ? null : Compile(branch.WhenFalse,depth+1));
        if (operation is IVariableDeclarationGroupOperation group && group.Declarations.Length==1 &&
            group.Declarations[0].Declarators.Length==1)
        {
            var declarator=group.Declarations[0].Declarators[0];
            var local=declarator.Symbol;
            if (local.RefKind!=RefKind.None || local.Type.SpecialType!=SpecialType.System_UInt32 || declarator.Initializer is null)
                throw new NotSupportedException("Initialized ordinary uint local required");
            var value=Lower(declarator.Initializer.Value,UInt32);
            // '$' cannot occur in a C# identifier; distinct Roslyn locals cannot alias a
            // parameter or a same-spelling local in a sibling lexical block.
            var name="$local"+localIndex++;
            var reference=new ClrParameter(name,UInt32);
            symbols.Add(local,reference);
            templateValues.Add(name,new(reference));
            return new Store(name,value);
        }
        if (operation is IExpressionStatementOperation {Operation:ISimpleAssignmentOperation assignment})
        {
            if (assignment.IsRef || assignment.Target is not ILocalReferenceOperation target ||
                target.Local.RefKind!=RefKind.None || !symbols.TryGetValue(target.Local,out var reference) ||
                reference is not ClrParameter local)
                throw new NotSupportedException("Assignment requires an existing ordinary uint local");
            return new Store(local.Name,Lower(assignment.Value,UInt32));
        }
        if (ContractCall(operation) is { } call)
        {
            if (call.TargetMethod.Name=="Require")
                throw new NotSupportedException("Require must occur in the method's top-level entry prefix");
            var (label,predicate)=ContractPredicate(call);
            var goal=new Goal(goals.Count,label,Line(operation),predicate);
            goals.Add(goal);
            results.Add(goal.Site,new());
            return goal;
        }
        if (operation is IReturnOperation {ReturnedValue:not null} ret)
            return new Exit(Lower(ret.ReturnedValue,UInt32));
        throw new NotSupportedException("Unsupported statement at line "+Line(operation)+": "+operation.Kind);
    }

    private static IInvocationOperation? ContractCall(IOperation operation)
        => operation is IExpressionStatementOperation {Operation:IInvocationOperation call} &&
           RuntimeSymbolPolicy.IsRuntimeType(call.TargetMethod.ContainingType,"SharpContracts.Contract") &&
           call.TargetMethod.Name is ("Require" or "Assert" or "Ensure") ? call : null;

    private (string Label,ClrExpression Predicate) ContractPredicate(IInvocationOperation call)
    {
        var arguments=call.Arguments.ToDictionary(a=>a.Parameter!.Name,a=>a.Value);
        if (!arguments.TryGetValue("condition",out var condition) ||
            !arguments.TryGetValue("id",out var id) || id.ConstantValue.Value is not string label ||
            string.IsNullOrWhiteSpace(label) || !arguments.TryGetValue("message",out var message) ||
            message.ConstantValue.Value is not string)
            throw new NotSupportedException("Constant nonblank ID and constant message required");
        return (label,Lower(condition,ClrScalarType.Boolean));
    }

    private ClrExpression Lower(IOperation operation,ClrScalarType expected)
    {
        // Bound source trees iteratively before the recursive lowerer/encoder runs.
        // Expanded snapshots need a second budget because updates can grow a small source tree.
        var pending=new Stack<(IOperation Operation,int Depth)>();
        pending.Push((operation,1));
        int count=0;
        while (pending.TryPop(out var item))
        {
            if (++count>MaximumExpressionSize || item.Depth>MaximumExpressionDepth)
                throw new NotSupportedException("Source expression budget exceeded");
            foreach (var child in item.Operation.ChildOperations)
                pending.Push((child,item.Depth+1));
        }
        var expression=lowerer.Lower(operation);
        if (expression.ValueType!=expected) throw new NotSupportedException("Unexpected statement expression type");
        Expand(expression,templateValues); // Check template complexity before calling the recursive encoder.
        encoder.Encode(expression); // Validate backend types even for an unreachable branch.
        return expression;
    }

    private static bool AlwaysReturns(Statement statement) => statement switch
    {
        Exit=>true,
        Block b=>b.Children.Length!=0 && AlwaysReturns(b.Children[^1]),
        Branch {No:not null} b=>AlwaysReturns(b.Yes) && AlwaysReturns(b.No),
        _=>false
    };

    private List<Path> Execute(IEnumerable<Statement> statements,List<Path> paths)
    {
        foreach (var statement in statements)
        {
            var next=new List<Path>();
            foreach (var path in paths)
            {
                switch (statement)
                {
                    case Store store:
                        var value=Expand(store.Expression,path.Values);
                        path.Values[store.Name]=value;
                        next.Add(path);
                        break;
                    case Block block:
                        next.AddRange(Execute(block.Children,new(){path}));
                        break;
                    case Exit exit:
                        Expand(exit.Expression,path.Values);
                        break; // A returned path never reaches a later source contract.
                    case Goal goal:
                        var predicate=(BoolExpr)encoder.Encode(Expand(goal.Predicate,path.Values).Expression);
                        solver.Push();
                        try
                        {
                            solver.Assert(path.Conditions.ToArray());
                            // Entry feasibility was checked separately. UNSAT here means an
                            // unreachable continuation, not inconsistent caller preconditions.
                            results[goal.Site].Add(ProofQuery.Check(context,solver,predicate,goal.Label,goal.Line,true,inputs,encoder));
                        }
                        finally { solver.Pop(); }
                        path.Conditions.Add(predicate); // Contract checks constrain normal fallthrough.
                        next.Add(path);
                        break;
                    case Branch branch:
                        if ((generatedPaths+=2)>MaximumGeneratedPaths)
                            throw new NotSupportedException("Generated symbolic path budget exceeded");
                        var condition=(BoolExpr)encoder.Encode(Expand(branch.Condition,path.Values).Expression);
                        AddArm(branch.Yes,path.Fork(condition),next);
                        AddArm(branch.No,path.Fork(context.MkNot(condition)),next);
                        break;
                    default:
                        throw new NotSupportedException("Unsupported compiled statement node");
                }
            }
            paths=next;
        }
        return paths;
    }

    private void AddArm(Statement? arm,Path path,List<Path> next)
    {
        solver.Push();
        Status reachability;
        try { solver.Assert(path.Conditions.ToArray()); reachability=solver.Check(); }
        finally { solver.Pop(); }
        if (reachability==Status.UNSATISFIABLE) return;
        // UNKNOWN is not pruned. The common query policy must still discharge each
        // obligation, and will retain UNKNOWN if feasibility cannot be established.
        next.AddRange(arm is null ? new(){path} : Execute(new[]{arm},new(){path}));
    }

    private MethodCheck Aggregate(Goal goal)
    {
        var checks=results[goal.Site];
        if (checks.Count==0) return new(goal.Label,goal.Line,VerificationStatus.Unknown,null,
            "Unreachable contract under entry assumptions or preceding checks; no nonvacuous proof");
        return checks.FirstOrDefault(c=>c.Status==VerificationStatus.Counterexample) ??
            checks.FirstOrDefault(c=>c.Status==VerificationStatus.Unknown) ?? checks[0];
    }

    private static Value Expand(ClrExpression expression,IReadOnlyDictionary<string,Value> values)
    {
        if (expression is ClrParameter parameter)
            return values.TryGetValue(parameter.Name,out var value) ? value :
                throw new NotSupportedException("Unbound symbolic local");
        if (expression is ClrConstant) return new(expression);
        Value result;
        if (expression is ClrBinary binary)
        {
            var left=Expand(binary.Left,values);
            var right=Expand(binary.Right,values);
            result=new(binary with {Left=left.Expression,Right=right.Expression},
                left.Size+right.Size+1,Math.Max(left.Depth,right.Depth)+1);
        }
        else if (expression is ClrUnary unary)
        {
            var operand=Expand(unary.Operand,values);
            result=new(unary with {Operand=operand.Expression},operand.Size+1,operand.Depth+1);
        }
        else if (expression is ClrConvert conversion)
        {
            var operand=Expand(conversion.Operand,values);
            result=new(conversion with {Operand=operand.Expression},operand.Size+1,operand.Depth+1);
        }
        else throw new NotSupportedException("Unsupported typed expression in statement template");
        if (result.Size>MaximumExpressionSize || result.Depth>MaximumExpressionDepth)
            throw new NotSupportedException("Expanded symbolic expression budget exceeded");
        return result;
    }

    private static int Line(IOperation operation)=>operation.Syntax.GetLocation().GetLineSpan().StartLinePosition.Line+1;
}
