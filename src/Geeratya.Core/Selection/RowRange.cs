namespace Geeratya.Core.Selection;

/// <summary>A run of rows by index, both ends included.</summary>
/// <param name="First">The first row of the run.</param>
/// <param name="Last">The last row of the run.</param>
public readonly record struct RowRange(int First, int Last)
{
    /// <summary>How many rows the run holds.</summary>
    public int Length => Last - First + 1;

    /// <summary>True when the row at <paramref name="index"/> is in the run.</summary>
    public bool Contains(int index)
    {
        return index >= First && index <= Last;
    }
}
