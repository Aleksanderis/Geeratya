using Geeratya.Core.Lists;

namespace Geeratya.Core.Boards;

/// <summary>
/// Scroll and paging of one column of cards of different height (<see cref="BoardCardPlan"/>). The scrolloff of
/// <see cref="ListMotion.ScrollOff"/> cards and the half page are counted in cards that really fit the rows, not in
/// the tallest card. Pure; the renderer places the cards through <see cref="BoardColumnLayout"/> with the same
/// heights.
/// </summary>
public static class BoardScroll
{
    /// <summary>The rows of card <paramref name="card"/> of <paramref name="group"/> in a cell of <paramref name="cellWidth"/>.</summary>
    public static int Height(BoardColumnGroup group, int card, BoardViewport viewport, int cellWidth)
    {
        if (viewport.Compact)
        {
            return 1;
        }

        return viewport.Width <= 0
            ? BoardCardPlan.MaxRows
            : BoardCardPlan.Height(group.Rows[card], BoardColumnLayout.PartOf(group, card), cellWidth);
    }

    /// <summary>
    /// The whole cards that fit the card rows from card <paramref name="first"/>, at least one while there is room
    /// at all and cards are left (a card taller than the area still counts as the one shown).
    /// </summary>
    public static int Fitting(BoardColumnGroup group, int first, BoardViewport viewport, int cellWidth)
    {
        var rows = viewport.CardRows;
        var used = 0;
        var count = 0;

        for (var card = Math.Max(0, first); card < group.Rows.Count; card++)
        {
            used += Height(group, card, viewport, cellWidth);

            if (used > rows)
            {
                break;
            }

            count++;
        }

        return rows > 0 && group.Rows.Count > first ? Math.Max(1, count) : count;
    }

    /// <summary>
    /// Moves the selection like <see cref="ListMotion.Move"/>; a half page is half the cards that fit from the
    /// current scroll.
    /// </summary>
    public static ListPosition Move(
        BoardColumnGroup group,
        ListPosition position,
        ListMove move,
        BoardViewport viewport,
        int cellWidth)
    {
        var count = group.Rows.Count;

        if (count <= 0)
        {
            return new ListPosition(0, 0);
        }

        var scroll = Math.Clamp(position.Scroll, 0, count - 1);
        var half = Math.Max(1, Fitting(group, scroll, viewport, cellWidth) / 2);

        var current = Math.Clamp(position.Selected, 0, count - 1);

        var selected = move switch
        {
            ListMove.Down => Math.Min(count - 1, current + 1),
            ListMove.Up => Math.Max(0, current - 1),
            ListMove.PageDown => Math.Min(count - 1, current + half),
            ListMove.PageUp => Math.Max(0, current - half),
            ListMove.Top => 0,
            _ => count - 1,
        };

        return Reveal(group, new ListPosition(selected, position.Scroll), viewport, cellWidth);
    }

    /// <summary>
    /// Adjusts the scroll so the selected card shows with up to <see cref="ListMotion.ScrollOff"/> cards of context on
    /// each side (fewer when they do not fit the rows), and so no blank rows show below the last card while earlier
    /// cards could fill them.
    /// </summary>
    public static ListPosition Reveal(BoardColumnGroup group, ListPosition position, BoardViewport viewport, int cellWidth)
    {
        var count = group.Rows.Count;
        var rows = viewport.CardRows;

        if (count <= 0 || rows <= 0)
        {
            return new ListPosition(Math.Clamp(position.Selected, 0, Math.Max(0, count - 1)), 0);
        }

        var selected = Math.Clamp(position.Selected, 0, count - 1);
        var context = Context(group, selected, viewport, cellWidth);
        var top = Math.Max(0, selected - context);
        var bottom = Math.Min(count - 1, selected + context);

        // The lowest scroll whose cards still reach the context below the selection.
        var lowest = bottom;
        var used = 0;

        while (lowest >= 0)
        {
            used += Height(group, lowest, viewport, cellWidth);

            if (used > rows)
            {
                break;
            }

            lowest--;
        }

        lowest = Math.Min(lowest + 1, selected);

        var scroll = Math.Max(lowest, Math.Min(position.Scroll, top));

        return new ListPosition(selected, Math.Min(scroll, Fill(group, viewport, cellWidth)));
    }

    /// <summary>The first card from which the cards down to the last one fit the rows (no blank rows below).</summary>
    private static int Fill(BoardColumnGroup group, BoardViewport viewport, int cellWidth)
    {
        var used = 0;
        var first = group.Rows.Count;

        while (first > 0)
        {
            used += Height(group, first - 1, viewport, cellWidth);

            if (used > viewport.CardRows)
            {
                break;
            }

            first--;
        }

        return first;
    }

    /// <summary>The most cards (up to the scrolloff) that fit on each side of the selected card together with it.</summary>
    private static int Context(BoardColumnGroup group, int selected, BoardViewport viewport, int cellWidth)
    {
        for (var context = ListMotion.ScrollOff; context > 0; context--)
        {
            var used = 0;

            for (var card = Math.Max(0, selected - context); card <= Math.Min(group.Rows.Count - 1, selected + context); card++)
            {
                used += Height(group, card, viewport, cellWidth);
            }

            if (used <= viewport.CardRows)
            {
                return context;
            }
        }

        return 0;
    }
}
