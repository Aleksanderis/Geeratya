using System.Globalization;
using Geeratya.Core.Text;
using Geeratya.Jira.Adf;

namespace Geeratya.Core.Editing;

/// <summary>
/// What the pointer does to the text of a selecting text area (the Summary and Description fields, the comment
/// editor): a click places the caret, a double-click selects a word, a drag selects the text between its ends. They
/// set <see cref="TextBuffer.Cursor"/> and <see cref="TextBuffer.Anchor"/> exactly as the select keys do, so the
/// selection copies, cuts and is replaced by typing like a keyboard one. A cell is a column counted from the left
/// edge of the text as drawn. Pure.
/// </summary>
public static class TextPointing
{
    private const string Ellipsis = "…";

    /// <summary>The caret at <paramref name="index"/>, nothing selected.</summary>
    public static TextBuffer Placed(TextBuffer buffer, int index)
    {
        return Moved(buffer, Math.Clamp(index, 0, buffer.Text.Length), null);
    }

    /// <summary>
    /// The text between <paramref name="from"/> and <paramref name="to"/> selected, the caret at <paramref name="to"/>.
    /// </summary>
    public static TextBuffer Dragged(TextBuffer buffer, int from, int to)
    {
        var anchor = Math.Clamp(from, 0, buffer.Text.Length);
        var cursor = Math.Clamp(to, 0, buffer.Text.Length);

        return Moved(buffer, cursor, anchor == cursor ? null : anchor);
    }

    /// <summary>
    /// The word at <paramref name="index"/> selected (a mention or issue reference as a whole; a run of spaces or of
    /// punctuation when the pointer is not on a word; the last character of a line when it is past its end).
    /// </summary>
    public static TextBuffer Word(TextBuffer buffer, int index)
    {
        var text = buffer.Text;
        var at = Math.Clamp(index, 0, text.Length);

        if (buffer.Tokens && TokenAt(text, at) is { } token)
        {
            return Dragged(buffer, token.Start, token.End);
        }

        if (at == text.Length || text[at] == '\n')
        {
            if (at == 0 || text[at - 1] == '\n')
            {
                return Placed(buffer, at);
            }

            at--;
        }

        var kind = KindOf(text[at]);
        var start = at;
        var end = at + 1;

        while (start > 0 && text[start - 1] != '\n' && KindOf(text[start - 1]) == kind)
        {
            start--;
        }

        while (end < text.Length && text[end] != '\n' && KindOf(text[end]) == kind)
        {
            end++;
        }

        return Dragged(buffer, start, end);
    }

    /// <summary>
    /// The text index under <paramref name="cell"/> of line <paramref name="line"/> (counted by line breaks) in a
    /// surface that does not wrap: the line is cut to <paramref name="width"/> around its caret (the line the caret is
    /// on) or around its end (every other line), with an ellipsis for hidden text, exactly as it is drawn.
    /// </summary>
    public static int IndexInLine(TextBuffer buffer, int line, int cell, int width)
    {
        var lines = buffer.Text.Split('\n');
        var row = Math.Clamp(line, 0, lines.Length - 1);
        var rowStart = lines.Take(row).Sum(static previous => previous.Length + 1);
        var text = lines[row];
        var caret = row == buffer.Line ? Math.Clamp(buffer.Column, 0, text.Length) : text.Length;
        var window = PromptWindow.Of(TextBuffer.FromBack(text, text.Length - caret), Math.Max(1, width));
        var hidden = !string.Equals(window.Before, text[..caret], StringComparison.Ordinal);
        var shownBefore = hidden ? window.Before[Ellipsis.Length..] : window.Before;
        var shownAfter = window.TailCut ? window.After[..^Ellipsis.Length] : window.After;
        var index = caret - shownBefore.Length;
        var used = hidden ? Ellipsis.Length : 0;

        if (cell < used)
        {
            return rowStart + index;
        }

        foreach (var cluster in Clusters($"{shownBefore}{shownAfter}"))
        {
            used += DisplayWidth.OfCluster(cluster);

            if (cell < used)
            {
                return rowStart + index;
            }

            index += cluster.Length;
        }

        return rowStart + index;
    }

    /// <summary>
    /// The text index under <paramref name="cell"/> of visual row <paramref name="row"/> of a wrapping surface (the
    /// comment editor) whose rows are laid out for <paramref name="width"/> columns.
    /// </summary>
    public static int IndexInRows(TextBuffer buffer, int row, int cell, int width)
    {
        var rows = EditorLayout.Rows(buffer.Text, EditorLayout.Capacity(width), buffer.Tokens);
        var clamped = Math.Clamp(row, 0, rows.Count - 1);

        return EditorLayout.IndexAt(buffer.Text, rows, clamped, Math.Max(0, cell), buffer.Tokens);
    }

    private static TextBuffer Moved(TextBuffer buffer, int cursor, int? anchor)
    {
        if (buffer.Cursor == cursor && buffer.Anchor == anchor)
        {
            return buffer;
        }

        // A pointer move ends the typed undo step, as a key move does.
        return buffer with { Cursor = cursor, Anchor = anchor, History = buffer.History.Broken() };
    }

    private static AdfTokenSpan? TokenAt(string text, int index)
    {
        foreach (var token in AdfMarkdownTokens.Find(text))
        {
            if (index >= token.Start && index < token.End)
            {
                return token;
            }
        }

        return null;
    }

    private static int KindOf(char value)
    {
        if (WordCharacters.IsWord(value))
        {
            return 0;
        }

        return char.IsWhiteSpace(value) ? 1 : 2;
    }

    private static List<string> Clusters(string text)
    {
        var clusters = new List<string>();
        var elements = StringInfo.GetTextElementEnumerator(text);

        while (elements.MoveNext())
        {
            clusters.Add(elements.GetTextElement());
        }

        return clusters;
    }
}
