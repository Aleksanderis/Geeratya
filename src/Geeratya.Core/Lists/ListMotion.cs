namespace Geeratya.Core.Lists;

/// <summary>
/// Selection and scroll of a list (<c>docs/internal/ux-screens.md</c>, "List layout"). Pure: the row count and the viewport
/// height are facts passed in, every result is clamped, and an empty list or a zero viewport never throws.
/// </summary>
public static class ListMotion
{
    /// <summary>Rows of context kept above and below the selection while scrolling (vim <c>scrolloff</c>).</summary>
    public const int ScrollOff = 3;

    /// <summary>
    /// Moves the selection. <paramref name="count"/> repeats a one-row or half-page move (a value below one counts
    /// as one); Top and Bottom ignore it. Scroll follows the selection (see <see cref="Reveal"/>).
    /// </summary>
    public static ListPosition Move(ListPosition position, ListMove move, int count, int rowCount, int viewport)
    {
        if (rowCount <= 0)
        {
            return new ListPosition(0, 0);
        }

        var times = Math.Max(1, count);
        var half = Math.Max(1, viewport / 2);
        var current = Math.Clamp(position.Selected, 0, rowCount - 1);

        var target = move switch
        {
            ListMove.Down => Add(current, times, 1),
            ListMove.Up => Add(current, times, -1),
            ListMove.PageDown => Add(current, times, half),
            ListMove.PageUp => Add(current, times, -half),
            ListMove.Top => 0,
            _ => rowCount - 1,
        };

        return Reveal(new ListPosition(Math.Clamp(target, 0, rowCount - 1), position.Scroll), rowCount, viewport);
    }

    /// <summary>
    /// Adjusts the scroll so the selection is visible with <see cref="ScrollOff"/> rows of context (fewer when the
    /// viewport is small), and so no blank rows show below the last row while earlier rows could fill them.
    /// </summary>
    public static ListPosition Reveal(ListPosition position, int rowCount, int viewport)
    {
        if (rowCount <= 0 || viewport <= 0)
        {
            return new ListPosition(Math.Clamp(position.Selected, 0, Math.Max(0, rowCount - 1)), 0);
        }

        var selected = Math.Clamp(position.Selected, 0, rowCount - 1);
        var context = Math.Min(ScrollOff, (viewport - 1) / 2);
        var scroll = position.Scroll;

        if (selected < scroll + context)
        {
            scroll = selected - context;
        }
        else if (selected > scroll + viewport - 1 - context)
        {
            scroll = selected - (viewport - 1 - context);
        }

        return new ListPosition(selected, Math.Clamp(scroll, 0, Math.Max(0, rowCount - viewport)));
    }

    private static int Add(int current, int times, int step)
    {
        var moved = current + ((long)times * step);

        return (int)Math.Clamp(moved, int.MinValue / 2, int.MaxValue / 2);
    }
}
