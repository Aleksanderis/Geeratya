using Geeratya.Core.Lists;

namespace Geeratya.Core.Boards;

/// <summary>
/// The room a board in the columns layout has: the rows of its card area and the width of the body. The cell width of
/// a column (and with it the height of every card) follows from the width, so scrolloff and paging count real cards.
/// A width of 0 (the host has not reported one yet) measures every full card at its tallest.
/// </summary>
/// <param name="VisibleRows">The list's visible rows (<see cref="Views.TabState.VisibleRows"/>).</param>
/// <param name="Width">The body width in cells, 0 while unknown.</param>
public readonly record struct BoardViewport(int VisibleRows, int Width)
{
    /// <summary>The rows between the top and the bottom edge of the frame.</summary>
    public int CardRows => BoardCards.CardRows(VisibleRows, ListLayout.HeaderRows);

    /// <summary>Whether cards are drawn on one line.</summary>
    public bool Compact => BoardCards.IsCompact(VisibleRows, ListLayout.HeaderRows);
}
