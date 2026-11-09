namespace Geeratya.Core.Boards;

/// <summary>
/// The body rows one lane's columns take (#891). A stacked lane starts with a header row; the board (frame, titles
/// and cards) is the rest, so the card-pitch maths are those of a board of <see cref="BoardRows"/> list rows.
/// </summary>
/// <param name="Key">The lane key (<see cref="SwimlaneLane.Key"/>); empty without a grouping.</param>
/// <param name="Top">The body row of the first row of the region.</param>
/// <param name="Rows">The rows of the region, header included.</param>
/// <param name="HasHeader">The region starts with a lane header row (stacked lanes only).</param>
/// <param name="Folded">The lane is folded: the region is its header row alone, with no board.</param>
public readonly record struct LaneRegion(string Key, int Top, int Rows, bool HasHeader, bool Folded = false)
{
    /// <summary>The body row where the board's top edge is.</summary>
    public int BoardTop => Top + (HasHeader ? LaneRegions.HeaderRows : 0);

    /// <summary>The rows of the board itself (top edge to bottom edge).</summary>
    public int BoardBodyRows => Math.Max(0, Rows - (HasHeader ? LaneRegions.HeaderRows : 0));

    /// <summary>The list rows the scroll maths count for the board (<see cref="Views.TabState.VisibleRows"/> terms).</summary>
    public int BoardRows => Math.Max(0, BoardBodyRows - Lists.ListLayout.HeaderRows - Lists.ListLayout.FooterRows);

    /// <summary>Whether a body row lies in the region.</summary>
    public bool Contains(int row)
    {
        return row >= Top && row < Top + Rows;
    }
}
