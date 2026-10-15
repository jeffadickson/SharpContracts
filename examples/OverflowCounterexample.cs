// Expected counterexample: int.MaxValue + 1 wraps in unchecked arithmetic.
using SharpContracts;

internal static class OverflowProofExamples
{
    public static void Check(int x)
    {
        Contract.Ensure(unchecked(x + 1) > x, "INT-001", "Addition always increases");
    }
}
