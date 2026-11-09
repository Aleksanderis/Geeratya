namespace Geeratya.Core.Boards;

/// <summary>The lane chips that fit a body width, and whether lanes lie beyond either end.</summary>
/// <param name="Chips">The drawn chips, left to right.</param>
/// <param name="MoreLeft">Lanes before the first chip are cut off (a <c>‹</c> is drawn at cell 0).</param>
/// <param name="MoreRight">Lanes after the last chip are cut off (a <c>›</c> is drawn in the last cell).</param>
public sealed record LaneStripLayout(IReadOnlyList<LaneStripChip> Chips, bool MoreLeft, bool MoreRight)
{
    /// <summary>The key of the lane whose chip covers body cell <paramref name="column"/>, or null.</summary>
    public string? KeyAt(int column)
    {
        return Chips.FirstOrDefault(chip => column >= chip.Start && column < chip.End)?.Key;
    }
}
