using System.Globalization;
using Geeratya.Core.Text;
using Geeratya.Jira.Adf;

namespace Geeratya.Core.Editing;

/// <summary>
/// Lays an edited text out in visual rows of a fixed capacity: a newline ends a row, a row that is full wraps after
/// its last space (a word longer than the row wraps by character; one space may hang past the end), so a cursor
/// index maps to one row and column. The last column of a row is kept
/// free for the cursor. Pure; the reducer (cursor moves) and the renderer use the same rows.
/// </summary>
public static class EditorLayout
{
    private const char Newline = '\n';

    /// <summary>The row capacity for a box of <paramref name="innerWidth"/> columns.</summary>
    public static int Capacity(int innerWidth)
    {
        return Math.Max(1, innerWidth - 1);
    }

    /// <summary>The rows of <paramref name="text"/>; always at least one (an empty text has one empty row).</summary>
    public static IReadOnlyList<EditorRow> Rows(string text, int capacity)
    {
        var rows = new List<EditorRow>();
        var limit = Math.Max(1, capacity);
        var rowStart = 0;
        var used = 0;
        var breakAt = -1;
        var usedAtBreak = 0;
        var cells = Cells(text);

        foreach (var cell in cells)
        {
            var index = cell.Start;
            var element = cell.Display;

            if (element[0] == Newline)
            {
                rows.Add(new EditorRow(rowStart, index));
                rowStart = index + 1;
                used = 0;
                breakAt = -1;

                continue;
            }

            var width = DisplayWidth.Of(element);
            var isSpace = element[0] == ' ';

            if (isSpace && used == limit)
            {
                // A space that does not fit hangs at the end of its row (in the column kept for the cursor).
                used += width;
                breakAt = index + 1;
                usedAtBreak = used;

                continue;
            }

            if (used > 0 && used + width > limit)
            {
                if (!isSpace && breakAt > rowStart)
                {
                    // Word wrap: the word that does not fit moves to the next row with the text after the last space.
                    rows.Add(new EditorRow(rowStart, breakAt));
                    rowStart = breakAt;
                    used -= usedAtBreak;
                }
                else
                {
                    rows.Add(new EditorRow(rowStart, index));
                    rowStart = index;
                    used = 0;
                }

                breakAt = -1;

                if (used > 0 && used + width > limit)
                {
                    rows.Add(new EditorRow(rowStart, index));
                    rowStart = index;
                    used = 0;
                }
            }

            used += width;

            if (isSpace)
            {
                breakAt = index + 1;
                usedAtBreak = used;
            }
        }

        rows.Add(new EditorRow(rowStart, text.Length));

        return rows;
    }

    /// <summary>
    /// The row and column of <paramref name="index"/>. An index at the end of a wrapped row belongs to the start of the
    /// next row; one before a newline, or at the end of the text, stays on its row.
    /// </summary>
    public static EditorPosition Locate(string text, IReadOnlyList<EditorRow> rows, int index)
    {
        var clamped = Math.Clamp(index, 0, text.Length);

        for (var row = 0; row < rows.Count; row++)
        {
            var current = rows[row];
            var isLast = row == rows.Count - 1;

            if (clamped < current.End || (clamped == current.End && (isLast || rows[row + 1].Start > current.End)))
            {
                var start = Math.Min(current.Start, clamped);

                return new EditorPosition(row, ColumnOf(text, start, clamped));
            }
        }

        return new EditorPosition(rows.Count - 1, 0);
    }

    /// <summary>
    /// The index in <paramref name="row"/> at the given display column, or the nearest character start before it; never
    /// the end of a wrapped row (that index belongs to the next row).
    /// </summary>
    public static int IndexAt(string text, IReadOnlyList<EditorRow> rows, int row, int column)
    {
        var current = rows[row];
        var used = 0;
        var index = current.Start;
        var lastStart = current.Start;

        foreach (var cell in RowCells(text, current))
        {
            var width = DisplayWidth.Of(cell.Display);

            if (used + width > column)
            {
                break;
            }

            used += width;
            lastStart = cell.Start;
            index = cell.End;
        }

        var wrapsOn = row < rows.Count - 1 && rows[row + 1].Start == current.End;

        if (wrapsOn && index == current.End && index > current.Start)
        {
            index = lastStart;
        }

        return index;
    }

    /// <summary>
    /// The cells of <paramref name="text"/>: one per character (grapheme), but a mention or issue link is one cell that
    /// shows its short form. The row layout, the cursor column and the renderer all measure these cells.
    /// </summary>
    public static IReadOnlyList<EditorCell> Cells(string text)
    {
        var cells = new List<EditorCell>(text.Length);
        var tokens = AdfMarkdownTokens.Find(text);
        var nextToken = 0;
        var skipUntil = 0;
        var elements = StringInfo.GetTextElementEnumerator(text);

        while (elements.MoveNext())
        {
            var index = elements.ElementIndex;

            if (index < skipUntil)
            {
                var tail = index + elements.GetTextElement().Length;

                // A mark glued to the end of a token is a cell of its own, never lost.
                if (tail > skipUntil)
                {
                    cells.Add(new EditorCell(skipUntil, tail, text[skipUntil..tail]));
                }

                continue;
            }

            if (nextToken < tokens.Count && tokens[nextToken].Start == index)
            {
                var token = tokens[nextToken++];

                cells.Add(new EditorCell(token.Start, token.End, AdfMarkdownTokens.Label(text, token)));
                skipUntil = token.End;

                continue;
            }

            var element = elements.GetTextElement();

            cells.Add(new EditorCell(index, index + element.Length, element));
        }

        return cells;
    }

    /// <summary>The cells of <paramref name="row"/>.</summary>
    public static IEnumerable<EditorCell> RowCells(string text, EditorRow row)
    {
        return Cells(text).Where(cell => cell.Start >= row.Start && cell.End <= row.End);
    }

    // The display width of the cells from start up to the cursor; a cursor inside a token stays at its start.
    private static int ColumnOf(string text, int start, int cursor)
    {
        return RowCells(text, new EditorRow(start, cursor)).Sum(cell => DisplayWidth.Of(cell.Display));
    }

    /// <summary>The start of the character before <paramref name="index"/>, or 0.</summary>
    public static int PreviousStart(string text, int index)
    {
        var previous = 0;

        foreach (var start in StringInfo.ParseCombiningCharacters(text))
        {
            if (start >= index)
            {
                break;
            }

            previous = start;
        }

        return previous;
    }

    /// <summary>The start of the character after <paramref name="index"/>, or the end of the text.</summary>
    public static int NextStart(string text, int index)
    {
        foreach (var start in StringInfo.ParseCombiningCharacters(text))
        {
            if (start > index)
            {
                return start;
            }
        }

        return text.Length;
    }
}
