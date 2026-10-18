using SharpContracts;

namespace SharpContracts.Examples;

/// <summary>Tests whether a new item batch fits in a bounded buffer.</summary>
public static class BufferAdmission
{
    /// <summary>Incorrect: overflowing addition can authorize an oversized write.</summary>
    [Verify]
    public static uint Buggy(uint used, uint incoming, uint capacity)
    {
        Contract.Require(used <= capacity, "BUFFER-PRE", "Existing usage fits capacity");
        Contract.Require(unchecked(used + incoming) <= capacity, "BUFFER-ADMIT", "Admission based on wrapped sum");
        uint requested = unchecked(used + incoming);
        Contract.Assert(incoming <= capacity - used, "BUFFER-FITS", "Accepted batch must fit without wraparound");
        return requested;
    }

    /// <summary>Correct admission check expressed as a property about the requested batch.
    /// This pedagogical method returns remaining capacity, not a write action.</summary>
    [Verify]
    public static uint Correct(uint used, uint incoming, uint capacity)
    {
        Contract.Require(used <= capacity, "BUFFER-PRE", "Existing usage fits capacity");
        Contract.Require(incoming <= capacity - used, "BUFFER-ADMIT", "Batch must fit remaining capacity");
        uint requested = unchecked(used + incoming);
        Contract.Ensure(requested <= capacity, "BUFFER-FITS", "Accepted batch fits capacity");
        return requested;
    }
}