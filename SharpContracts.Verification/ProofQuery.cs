using System.Globalization;
using System.Numerics;
using Microsoft.CodeAnalysis;
using Microsoft.Z3;
using SharpContracts.Solver;
using SharpContracts.Symbolic;

namespace SharpContracts.Verification;

/// <summary>Common feasibility, SAT/UNSAT/UNKNOWN and counterexample policy for the bound frontends.</summary>
internal static class ProofQuery
{
    internal static MethodCheck Check(Context context, Microsoft.Z3.Solver solver, BoolExpr predicate,
        string contractId, int line, bool hasPriorCheck,
        IEnumerable<KeyValuePair<ISymbol, ClrExpression>> inputs, ClrExpressionEncoder encoder)
    {
        var feasibility = solver.Check();
        if (feasibility == Status.UNSATISFIABLE)
        {
            if (!hasPriorCheck) throw new NotSupportedException("Inconsistent preconditions: no permitted inputs");
            // A failing earlier runtime check can block all normal execution paths. Preserve
            // its counterexample, rather than relabeling it as inconsistent caller assumptions.
            return new(contractId, line, VerificationStatus.Unknown, null,
                "Unreachable after preceding contract failures; no nonvacuous proof");
        }
        if (feasibility == Status.UNKNOWN)
            return new(contractId, line, VerificationStatus.Unknown, null,
                "Precondition feasibility unknown: " + solver.ReasonUnknown);
        solver.Push();
        try
        {
            solver.Assert(context.MkNot(predicate));
            var answer = solver.Check();
            return new(contractId, line,
                answer == Status.UNSATISFIABLE ? VerificationStatus.Proved :
                answer == Status.SATISFIABLE ? VerificationStatus.Counterexample : VerificationStatus.Unknown,
                answer == Status.SATISFIABLE ? solver.Model.ToString() : null,
                answer == Status.UNKNOWN ? solver.ReasonUnknown : null,
                answer == Status.SATISFIABLE ? inputs.ToDictionary(p => p.Key.Name,
                    p => InputValue(solver.Model, encoder, p.Value)) : null);
        }
        finally { solver.Pop(); }
    }

    private static string InputValue(Model model, ClrExpressionEncoder encoder, ClrExpression expression)
    {
        var bits = ((BitVecNum)model.Evaluate(encoder.Encode(expression), true)).BigInteger;
        var type = expression.ValueType;
        if (type.IsSigned && bits >= (BigInteger.One << (type.Width - 1)))
            bits -= BigInteger.One << type.Width;
        return bits.ToString(CultureInfo.InvariantCulture);
    }
}
