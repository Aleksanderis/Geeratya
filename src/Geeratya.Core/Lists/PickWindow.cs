namespace Geeratya.Core.Lists;

/// <summary>The rows of a pick list that fit the viewport: the first one drawn and how many are drawn.</summary>
/// <param name="First">Index of the first row drawn.</param>
/// <param name="Shown">How many rows are drawn (0 for an empty list or a zero viewport).</param>
public readonly record struct PickWindow(int First, int Shown)
{
    /// <summary>The indexes of the rows drawn, in order.</summary>
    public IEnumerable<int> Indexes => Enumerable.Range(First, Shown);
}
