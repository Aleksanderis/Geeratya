namespace Geeratya.Core.Lists;

/// <summary>
/// The one cursor of every list: clamping, steps, pages, ends and the scroll that keeps the selection in view
/// (<c>docs/internal/ui-consistency.md</c>, component 4). The lists, the pick lists, the popups and the board
/// columns all move and scroll through it, so they share one scroll margin (<see cref="ScrollOff"/>) and one
/// half-page rule (<see cref="Half"/>). Pure: the row count and the viewport height are facts passed in, every
/// result is clamped, and an empty list or a zero viewport never throws.
/// </summary>
public static class ListCursor
{
    /// <summary>Rows of context kept above and below the selection while scrolling (vim <c>scrolloff</c>).</summary>
    public const int ScrollOff = 3;

    /// <summary>
    /// <paramref name="index"/> kept inside a list of <paramref name="count"/> rows (0 for an empty list).
    /// </summary>
    public static int Clamp(int index, int count)
    {
        return Math.Clamp(index, 0, Math.Max(0, count - 1));
    }

    /// <summary>
    /// <paramref name="index"/> moved by <paramref name="delta"/> rows and kept inside the list; a huge delta cannot
    /// overflow.
    /// </summary>
    public static int Step(int index, int delta, int count)
    {
        return Clamp((int)Math.Clamp((long)index + delta, int.MinValue / 2, int.MaxValue / 2), count);
    }

    /// <summary>A half page: half the viewport, at least one row.</summary>
    public static int Half(int viewport)
    {
        return Math.Max(1, viewport / 2);
    }

    /// <summary>
    /// The row a move leads to from <paramref name="current"/>. <paramref name="times"/> repeats a one-row or half-page
    /// move (a value below one counts as one); Top and Bottom ignore it. <paramref name="half"/> is the rows of a half
    /// page (<see cref="Half"/>, or the cards that fit for cards of several rows).
    /// </summary>
    public static int Target(int current, ListMove move, int times, int count, int half)
    {
        var repeat = Math.Max(1, times);
        var at = Clamp(current, count);
        var page = (int)Math.Min(int.MaxValue / 4, (long)repeat * half);

        return move switch
        {
            ListMove.Down => Step(at, repeat, count),
            ListMove.Up => Step(at, -repeat, count),
            ListMove.PageDown => Step(at, page, count),
            ListMove.PageUp => Step(at, -page, count),
            ListMove.Top => 0,
            _ => Clamp(count - 1, count),
        };
    }

    /// <summary>Moves the selection; the scroll follows it (see <see cref="Reveal"/>).</summary>
    public static ListPosition Move(ListPosition position, ListMove move, int times, int rowCount, int viewport)
    {
        if (rowCount <= 0)
        {
            return new ListPosition(0, 0);
        }

        var target = Target(position.Selected, move, times, rowCount, Half(viewport));

        return Reveal(new ListPosition(target, position.Scroll), rowCount, viewport);
    }

    /// <summary>
    /// Adjusts the scroll so the selection is visible with <see cref="ScrollOff"/> rows of context (fewer when the
    /// viewport is small), and so no blank rows show below the last row while earlier rows could fill them.
    /// </summary>
    public static ListPosition Reveal(ListPosition position, int rowCount, int viewport)
    {
        if (rowCount <= 0 || viewport <= 0)
        {
            return new ListPosition(Clamp(position.Selected, rowCount), 0);
        }

        var selected = Clamp(position.Selected, rowCount);
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

    /// <summary>
    /// The first row to draw for a list that keeps no scroll of its own (a popup or a picker drawn from its selection
    /// alone): the selection stays inside the window with the same margin below it as <see cref="Reveal"/> keeps, and
    /// the window never shows blank rows below the last row.
    /// </summary>
    public static int FirstVisible(int selected, int rowCount, int viewport)
    {
        return Reveal(new ListPosition(selected, 0), rowCount, viewport).Scroll;
    }
}
