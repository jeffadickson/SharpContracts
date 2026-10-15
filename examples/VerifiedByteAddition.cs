// Test with: dotnet run --project SharpContracts.Cli -- examples/VerifiedByteAddition.cs Check
using SharpContracts;

internal static class NumericProofExamples
{
    public static void Check(byte a, byte b)
    {
        Contract.Ensure(a + b >= a, "BYTE-001", "Promoted sum does not decrease");
    }
}
