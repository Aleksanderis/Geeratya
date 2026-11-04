namespace Geeratya.Core.Detail;

/// <summary>How much of a long text shows: its line count and the width the notes under it wrap to.</summary>
/// <param name="Lines">Lines shown before the rest is cut.</param>
/// <param name="Width">The columns inside the panel.</param>
internal sealed record LongTextLimit(int Lines, int Width);
