using SharpContracts;

namespace SharpContracts.Examples;

/// <summary>
/// Inline-verifiable binary-search midpoint implementations.
/// Explicitly models unchecked UInt32 arithmetic; valid interval is low less than or equal to high.
/// </summary>
public static class BinarySearchMidpoint
{
    /// <summary>Buggy because low + high can overflow before unsigned division.</summary>
    [Verify]
    public static uint Buggy(uint low, uint high)
    {
        Contract.Require(low <= high, "MID-PRE", "Valid search interval");
        uint midpoint = unchecked((low + high) / 2u);
        Contract.Ensure(midpoint >= low, "MID-LOW", "Midpoint cannot be below low");
        Contract.Ensure(midpoint <= high, "MID-HIGH", "Midpoint cannot exceed high");
        return midpoint;
    }

    /// <summary>Correct for all UInt32 low and high with low less than or equal to high.</summary>
    [Verify]
    public static uint Correct(uint low, uint high)
    {
        Contract.Require(low <= high, "MID-PRE", "Valid search interval");
        uint midpoint = unchecked(low + (high - low) / 2u);
        Contract.Assert(midpoint >= low, "MID-LOW", "Midpoint cannot be below low");
        Contract.Ensure(midpoint <= high, "MID-HIGH", "Midpoint cannot exceed high");
        return midpoint;
    }
}
