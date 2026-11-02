using Geeratya.Core.Lists;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Boards;

/// <summary>
/// Selection and scroll on a board in the columns layout (<c>docs/ux-screens.md</c>, "Columns layout"). Pure: the
/// columns, the cursor and the number of cards a column shows are facts passed in; every result is clamped and an
/// empty board or column never throws.
/// </summary>
public static class BoardMotion
{
    /// <summary>
    /// Makes <paramref name="cursor"/> fit <paramref name="columns"/>: arrays sized to the columns, the selected card
    /// clamped, and the selected column scrolled so the card shows. Without a cursor the card with
    /// <paramref name="selectedKey"/> is found; with one, that key only refines the card inside the selected column
    /// (a refresh may have moved it), so a column the user chose never jumps away.
    /// </summary>
    public static BoardCursor Resolve(
        IReadOnlyList<BoardColumnGroup> columns,
        BoardCursor? cursor,
        string? selectedKey,
        int cardsVisible)
    {
        if (columns.Count == 0)
        {
            return new BoardCursor(0, [], [], 0);
        }

        var cards = new int[columns.Count];
        var scrolls = new int[columns.Count];

        Copy(cursor?.Cards, cards);
        Copy(cursor?.Scrolls, scrolls);

        var column = cursor is null ? 0 : Math.Clamp(cursor.Column, 0, columns.Count - 1);
        var wanted = cursor?.Wanted ?? 0;

        if (cursor is null && FindKey(columns, selectedKey) is { } found)
        {
            column = found.Column;
            cards[column] = found.Card;
            wanted = found.Card;
        }
        else if (cursor is not null && IndexOfKey(columns[column].Rows, selectedKey) is var index && index >= 0)
        {
            wanted = index == cards[column] ? wanted : index;
            cards[column] = index;
        }

        for (var each = 0; each < columns.Count; each++)
        {
            var count = columns[each].Rows.Count;

            cards[each] = Math.Clamp(cards[each], 0, Math.Max(0, count - 1));
            scrolls[each] = ListMotion.Reveal(new ListPosition(cards[each], scrolls[each]), count, cardsVisible).Scroll;
        }

        return new BoardCursor(column, cards, scrolls, Math.Max(0, wanted));
    }

    /// <summary>
    /// Selects the card with <paramref name="key"/> wherever it is now (a card whose status changed sits in another
    /// column) and keeps the scroll offsets of every column. A key that is not on the board leaves the cursor alone.
    /// </summary>
    public static BoardCursor Follow(
        IReadOnlyList<BoardColumnGroup> columns,
        BoardCursor cursor,
        string key,
        int cardsVisible)
    {
        var current = Resolve(columns, cursor, null, cardsVisible);

        if (FindKey(columns, key) is not { } found)
        {
            return current;
        }

        var position = ListMotion.Reveal(
            new ListPosition(found.Card, current.Scrolls[found.Column]),
            columns[found.Column].Rows.Count,
            cardsVisible);

        return With(current, found.Column, position, found.Card);
    }

    /// <summary>The issue under the cursor; null when the selected column is empty.</summary>
    public static IssueSummary? SelectedIssue(IReadOnlyList<BoardColumnGroup> columns, BoardCursor cursor)
    {
        if (cursor.Column < 0 || cursor.Column >= columns.Count)
        {
            return null;
        }

        var rows = columns[cursor.Column].Rows;

        return cursor.Card < rows.Count ? rows[cursor.Card] : null;
    }

    /// <summary>Moves the selection inside the selected column; an empty column stays as it is.</summary>
    public static BoardCursor MoveCard(
        IReadOnlyList<BoardColumnGroup> columns,
        BoardCursor? cursor,
        string? selectedKey,
        ListMove move,
        int cardsVisible)
    {
        var current = Resolve(columns, cursor, selectedKey, cardsVisible);

        if (columns.Count == 0)
        {
            return current;
        }

        var count = columns[current.Column].Rows.Count;
        var position = ListMotion.Move(
            new ListPosition(current.Card, current.Scrolls[current.Column]),
            move,
            1,
            count,
            cardsVisible);

        return With(current, current.Column, position, position.Selected);
    }

    /// <summary>
    /// Moves to another column and keeps the card index, clamped to the last card of the target (an empty column can
    /// be selected). Stops at the ends.
    /// </summary>
    public static BoardCursor MoveColumn(
        IReadOnlyList<BoardColumnGroup> columns,
        BoardCursor? cursor,
        string? selectedKey,
        BoardColumnMove move,
        int cardsVisible)
    {
        var current = Resolve(columns, cursor, selectedKey, cardsVisible);

        if (columns.Count == 0)
        {
            return current;
        }

        var target = move switch
        {
            BoardColumnMove.Left => current.Column - 1,
            BoardColumnMove.Right => current.Column + 1,
            BoardColumnMove.First => 0,
            _ => columns.Count - 1,
        };

        target = Math.Clamp(target, 0, columns.Count - 1);

        var count = columns[target].Rows.Count;
        var card = Math.Clamp(current.Wanted, 0, Math.Max(0, count - 1));
        var position = ListMotion.Reveal(new ListPosition(card, current.Scrolls[target]), count, cardsVisible);

        return With(current, target, position, current.Wanted);
    }

    /// <summary>
    /// Puts the cursor on a column and card (a click). Without a card the column keeps the card it remembers; the
    /// chosen card becomes the one a later column change aims at.
    /// </summary>
    public static BoardCursor Place(
        IReadOnlyList<BoardColumnGroup> columns,
        BoardCursor? cursor,
        string? selectedKey,
        BoardHit hit,
        int cardsVisible)
    {
        var current = Resolve(columns, cursor, selectedKey, cardsVisible);

        if (columns.Count == 0)
        {
            return current;
        }

        var column = Math.Clamp(hit.Column, 0, columns.Count - 1);
        var count = columns[column].Rows.Count;
        var card = Math.Clamp(hit.Card ?? current.Cards[column], 0, Math.Max(0, count - 1));
        var position = ListMotion.Reveal(new ListPosition(card, current.Scrolls[column]), count, cardsVisible);

        return With(current, column, position, hit.Card is null ? current.Wanted : card);
    }

    private static BoardCursor With(BoardCursor current, int column, ListPosition position, int wanted)
    {
        var cards = current.Cards.ToArray();
        var scrolls = current.Scrolls.ToArray();

        cards[column] = position.Selected;
        scrolls[column] = position.Scroll;

        return new BoardCursor(column, cards, scrolls, wanted);
    }

    private static void Copy(IReadOnlyList<int>? source, int[] target)
    {
        if (source is null)
        {
            return;
        }

        for (var index = 0; index < Math.Min(source.Count, target.Length); index++)
        {
            target[index] = source[index];
        }
    }

    private static BoardPosition? FindKey(IReadOnlyList<BoardColumnGroup> columns, string? key)
    {
        if (key is null)
        {
            return null;
        }

        for (var column = 0; column < columns.Count; column++)
        {
            var index = IndexOfKey(columns[column].Rows, key);

            if (index >= 0)
            {
                return new BoardPosition(column, index);
            }
        }

        return null;
    }

    private static int IndexOfKey(IReadOnlyList<IssueSummary> rows, string? key)
    {
        if (key is null)
        {
            return -1;
        }

        for (var index = 0; index < rows.Count; index++)
        {
            if (rows[index].Key == key)
            {
                return index;
            }
        }

        return -1;
    }
}
