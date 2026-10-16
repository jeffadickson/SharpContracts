namespace SharpContracts.Examples;

/// <summary>
/// Binary search midpoint arithmetic for unsigned 32-bit offsets.
/// Precondition: low &lt;= high. Both methods operate in unchecked arithmetic.
/// </summary>
public static class BinarySearchMidpoint
{
    /// <summary>Incorrect: low + high can wrap before division by two.</summary>
    public static uint Buggy(uint low, uint high) =>
        unchecked((low + high) / 2u);

    /// <summary>Correct for low &lt;= high: subtraction cannot underflow, and addition cannot overflow.</summary>
    public static uint Correct(uint low, uint high) =>
        unchecked(low + (high - low) / 2u);
}
