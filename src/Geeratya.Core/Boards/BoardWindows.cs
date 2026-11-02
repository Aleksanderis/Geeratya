namespace Geeratya.Core.Boards;

/// <summary>Chooses the columns that fit (<c>docs/ux-screens.md</c>, "Columns layout"). Pure.</summary>
public static class BoardWindows
{
    /// <summary>The narrowest a column gets; fewer columns are drawn rather than narrower ones.</summary>
    public const int MinimumColumnWidth = 20;

    /// <summary>Below this terminal width one column is drawn at a time, with its neighbours named in the header.</summary>
    public const int SingleColumnBelow = 60;

    /// <summary>
    /// As many columns as fit at <see cref="MinimumColumnWidth"/> (at least one; exactly one below
    /// <see cref="SingleColumnBelow"/>), centred on <paramref name="selected"/> and kept inside the board.
    /// </summary>
    public static BoardWindow For(int total, int selected, int width)
    {
        if (total <= 0)
        {
            return new BoardWindow(0, 0, 0, Math.Max(0, width));
        }

        var fitting = width < SingleColumnBelow ? 1 : width / MinimumColumnWidth;
        var count = Math.Clamp(fitting, 1, total);
        var chosen = Math.Clamp(selected, 0, total - 1);
        var first = Math.Clamp(chosen - ((count - 1) / 2), 0, total - count);

        return new BoardWindow(first, count, total, Math.Max(0, width));
    }
}
