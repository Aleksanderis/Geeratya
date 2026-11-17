namespace Geeratya.Core.Lists;

/// <summary>
/// Selection and scroll of a list (<c>docs/internal/ux-screens.md</c>, "List layout"), a facade over
/// <see cref="ListCursor"/>, which holds the rules. Pure: an empty list or a zero viewport never throws.
/// </summary>
public static class ListMotion
{
    /// <summary>Rows of context kept above and below the selection while scrolling (vim <c>scrolloff</c>).</summary>
    public const int ScrollOff = ListCursor.ScrollOff;

    /// <summary>
    /// Moves the selection. <paramref name="count"/> repeats a one-row or half-page move (a value below one counts
    /// as one); Top and Bottom ignore it. Scroll follows the selection (see <see cref="Reveal"/>).
    /// </summary>
    public static ListPosition Move(ListPosition position, ListMove move, int count, int rowCount, int viewport)
    {
        return ListCursor.Move(position, move, count, rowCount, viewport);
    }

    /// <summary>
    /// Adjusts the scroll so the selection is visible with <see cref="ScrollOff"/> rows of context (fewer when the
    /// viewport is small), and so no blank rows show below the last row while earlier rows could fill them.
    /// </summary>
    public static ListPosition Reveal(ListPosition position, int rowCount, int viewport)
    {
        return ListCursor.Reveal(position, rowCount, viewport);
    }
}
