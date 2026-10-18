using SharpContracts;

namespace SharpContracts.Examples;

/// <summary>Splits an unsigned number of cents between two recipients.</summary>
public static class PaymentSplit
{
    /// <summary>Incorrect for odd cent amounts: the remainder is lost.</summary>
    [Verify]
    public static uint Buggy(uint cents)
    {
        uint first = cents / 2u;
        uint second = cents / 2u;
        Contract.Ensure(first + second == cents,
            "SPLIT-CONSERVE", "Every cent must be allocated");
        return first;
    }

    /// <summary>Conserves every cent, including odd amounts and UInt32.MaxValue.</summary>
    [Verify]
    public static uint Correct(uint cents)
    {
        uint first = cents / 2u;
        uint second = cents - first;
        Contract.Ensure(first + second == cents,
            "SPLIT-CONSERVE", "Every cent must be allocated");
        return first;
    }
}