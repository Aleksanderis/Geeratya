using System.Globalization;
using Geeratya.Core.Text;

namespace Geeratya.Core.Editing;

/// <summary>
/// Lays an edited text out in visual rows of a fixed capacity: a newline ends a row, a row that is full wraps after
/// its last space (a word longer than the row wraps by character), so a cursor index maps to one row and column. The last column of a row is kept
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
        var elements = StringInfo.GetTextElementEnumerator(text);

        while (elements.MoveNext())
        {
            var index = elements.ElementIndex;
            var element = elements.GetTextElement();

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

                return new EditorPosition(row, DisplayWidth.Of(text[start..clamped]));
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
        var elements = StringInfo.GetTextElementEnumerator(text[current.Start..current.End]);

        while (elements.MoveNext())
        {
            var element = elements.GetTextElement();
            var width = DisplayWidth.Of(element);

            if (used + width > column)
            {
                break;
            }

            used += width;
            index += element.Length;
        }

        var wrapsOn = row < rows.Count - 1 && rows[row + 1].Start == current.End;

        if (wrapsOn && index == current.End && index > current.Start)
        {
            index = PreviousStart(text, index);
        }

        return index;
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
