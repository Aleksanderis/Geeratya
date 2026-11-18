using Geeratya.Core.Text;
using Geeratya.Jira.Adf;

namespace Geeratya.Core.Editing;

/// <summary>
/// A text with a cursor (a UTF-16 index at the start of a character) and the edits every text surface shares: the
/// palette, pickers, jump prompt, filters, form fields, the profile form and the comment editor. Immutable: every
/// edit returns a new buffer. The cursor moves by character (grapheme), never inside a surrogate pair or a combining
/// sequence, and a mention or issue reference counts as one character. Up, Down, Home and End move by visual row, so
/// they need the row capacity the text is laid out with (<see cref="NoWrap"/> for a surface that does not wrap: its
/// rows are its lines). <see cref="Apply"/> maps every <see cref="EditorOperation"/> onto the edits below.
/// </summary>
/// <param name="Text">The text; lines are separated by <c>\n</c>.</param>
/// <param name="Cursor">The index of the character the cursor is before; the text length at the end.</param>
/// <param name="Tokens">
/// Whether a mention or issue reference (<see cref="AdfMarkdownTokens"/>) is one character: only the comment editor,
/// which shows them as short labels, sets it. Every other surface shows its text as it is and edits it that way.
/// </param>
public sealed record TextBuffer(string Text, int Cursor, bool Tokens = false)
{
    /// <summary>The most characters a buffer takes (Jira refuses a text above 32 767).</summary>
    public const int MaxLength = 32767;

    /// <summary>The row capacity of a surface that does not wrap: a row is a line.</summary>
    public const int NoWrap = int.MaxValue / 2;

    public static TextBuffer Empty { get; } = new(string.Empty, 0);

    /// <summary>An empty buffer for the comment editor (<see cref="Tokens"/>).</summary>
    public static TextBuffer EmptyRich { get; } = new(string.Empty, 0, true);

    /// <summary>A buffer with <paramref name="text"/> and the cursor at its end.</summary>
    public static TextBuffer Of(string text)
    {
        return new TextBuffer(text, text.Length);
    }

    /// <summary>The comment editor's buffer: the text, the cursor at its end, mentions as one character.</summary>
    public static TextBuffer Rich(string text)
    {
        return new TextBuffer(text, text.Length, true);
    }

    /// <summary>
    /// A buffer whose cursor is <paramref name="back"/> UTF-16 units before the end of <paramref name="text"/> (clamped
    /// to the text): the form states keep the caret this way, so a text that is set or appended to elsewhere keeps
    /// its caret at the end (0). A caret inside a character moves to its start.
    /// </summary>
    public static TextBuffer FromBack(string text, int back)
    {
        var cursor = text.Length - Math.Clamp(back, 0, text.Length);

        return new TextBuffer(text, cursor == text.Length ? cursor : EditorLayout.StartAtOrBefore(text, cursor));
    }

    /// <summary>The units between the cursor and the end of the text; see <see cref="FromBack"/>.</summary>
    public int Back => Text.Length - Math.Clamp(Cursor, 0, Text.Length);

    /// <summary>The number of line breaks before the cursor: its line (0 for a single line).</summary>
    public int Line => Text[..Math.Clamp(Cursor, 0, Text.Length)].Count(static character => character == '\n');

    /// <summary>The cursor's column in its line, in UTF-16 units.</summary>
    public int Column => Math.Clamp(Cursor, 0, Text.Length) - LineStart();

    /// <summary>Applies an editing key; the same buffer when it changes nothing.</summary>
    public TextBuffer Apply(EditorOperation operation, int capacity = NoWrap, int page = 1)
    {
        return operation switch
        {
            EditorOperation.Left => Left(),
            EditorOperation.Right => Right(),
            EditorOperation.Up => Vertical(-1, capacity),
            EditorOperation.Down => Vertical(1, capacity),
            EditorOperation.PageUp => Vertical(-page, capacity),
            EditorOperation.PageDown => Vertical(page, capacity),
            EditorOperation.RowStart => RowStart(capacity),
            EditorOperation.RowEnd => RowEnd(capacity),
            EditorOperation.WordLeft => WordLeft(),
            EditorOperation.WordRight => WordRight(),
            EditorOperation.Backspace => Backspace(),
            EditorOperation.Delete => Delete(),
            EditorOperation.DeleteWordBefore => DeleteBetween(WordLeft().Cursor, Cursor),
            EditorOperation.DeleteWordAfter => DeleteBetween(Cursor, WordRight().Cursor),
            EditorOperation.DeleteToStart => DeleteBetween(LineStart(), Cursor),
            EditorOperation.DeleteToEnd => DeleteBetween(Cursor, LineEnd()),
            _ => this,
        };
    }

    /// <summary>Inserts at the cursor; text beyond <see cref="MaxLength"/> is dropped.</summary>
    public TextBuffer Insert(string typed)
    {
        var room = MaxLength - Text.Length;

        if (typed.Length == 0 || room <= 0)
        {
            return this;
        }

        // A cursor moved by rows can sit inside a token: typing goes after it, never into it.
        if (TokenStartingAt(Cursor) is { } token && token.Start < Cursor)
        {
            return (this with { Cursor = token.End }).Insert(typed);
        }

        var accepted = typed.Length > room ? Truncated(typed, room) : typed;

        return this with { Text = Text.Insert(Cursor, accepted), Cursor = Cursor + accepted.Length };
    }

    /// <summary>Removes the character before the cursor.</summary>
    public TextBuffer Backspace()
    {
        if (Cursor == 0)
        {
            return this;
        }

        if (TokenEndingAt(Cursor) is { } token)
        {
            return this with { Text = Text.Remove(token.Start, token.End - token.Start), Cursor = token.Start };
        }

        var start = EditorLayout.PreviousStart(Text, Cursor);

        return this with { Text = Text.Remove(start, Cursor - start), Cursor = start };
    }

    /// <summary>The text with <c>[start, end)</c> replaced by <paramref name="replacement"/>, the cursor after it.</summary>
    public TextBuffer Replace(int start, int end, string replacement)
    {
        var text = $"{Text[..start]}{replacement}{Text[end..]}";

        return text.Length > MaxLength ? this : this with { Text = text, Cursor = start + replacement.Length };
    }

    /// <summary>Removes the character under the cursor.</summary>
    public TextBuffer Delete()
    {
        if (Cursor >= Text.Length)
        {
            return this;
        }

        if (TokenStartingAt(Cursor) is { } token)
        {
            return this with { Text = Text.Remove(token.Start, token.End - token.Start), Cursor = token.Start };
        }

        return this with { Text = Text.Remove(Cursor, EditorLayout.NextStart(Text, Cursor) - Cursor) };
    }

    /// <summary>
    /// Removes <c>[start, end)</c>, the cursor at the start; nothing when the range is empty. A mention or issue
    /// reference the range touches is removed whole, never cut in half.
    /// </summary>
    public TextBuffer DeleteBetween(int start, int end)
    {
        if (end <= start)
        {
            return this;
        }

        foreach (var span in TokensOf())
        {
            if (span.Start < end && span.End > start)
            {
                start = Math.Min(start, span.Start);
                end = Math.Max(end, span.End);
            }
        }

        return this with { Text = Text.Remove(start, end - start), Cursor = start };
    }

    /// <summary>The index of the start of the line (up to the previous line break) the cursor is on.</summary>
    public int LineStart()
    {
        return Cursor == 0 ? 0 : Text.LastIndexOf('\n', Cursor - 1) + 1;
    }

    /// <summary>The index of the end of the line (up to the next line break) the cursor is on.</summary>
    public int LineEnd()
    {
        var end = Text.IndexOf('\n', Math.Min(Cursor, Text.Length));

        return end < 0 ? Text.Length : end;
    }

    /// <summary>One character left; a mention or issue reference counts as one character.</summary>
    public TextBuffer Left()
    {
        var target = TokenEndingAt(Cursor) is { } token ? token.Start : EditorLayout.PreviousStart(Text, Cursor);

        return this with { Cursor = target };
    }

    /// <summary>One character right; a mention or issue reference counts as one character.</summary>
    public TextBuffer Right()
    {
        var target = TokenStartingAt(Cursor) is { } token ? token.End : EditorLayout.NextStart(Text, Cursor);

        return this with { Cursor = target };
    }

    /// <summary>
    /// <paramref name="direction"/> visual rows up (negative) or down, keeping the display column where the row is
    /// long enough; past the first or last row it goes to the start or end of the text.
    /// </summary>
    public TextBuffer Vertical(int direction, int capacity)
    {
        var rows = EditorLayout.Rows(Text, capacity, Tokens);
        var position = EditorLayout.Locate(Text, rows, Cursor, Tokens);
        var target = position.Row + direction;

        // Past the first row the cursor goes to the start of the text, past the last row to its end, as an editor does.
        if (target < 0)
        {
            return this with { Cursor = 0 };
        }

        if (target >= rows.Count)
        {
            return this with { Cursor = Text.Length };
        }

        if (target == position.Row)
        {
            return this;
        }

        return this with { Cursor = EditorLayout.IndexAt(Text, rows, target, position.Column, Tokens) };
    }

    /// <summary>The start of the word before the cursor (spaces and punctuation skipped first), or the text start.</summary>
    public TextBuffer WordLeft()
    {
        var index = Cursor;

        while (index > 0 && !WordCharacters.IsWord(Text[index - 1]))
        {
            index--;
        }

        while (index > 0 && WordCharacters.IsWord(Text[index - 1]))
        {
            index--;
        }

        return this with { Cursor = index };
    }

    /// <summary>The end of the word at or after the cursor (spaces and punctuation skipped first), or the text end.</summary>
    public TextBuffer WordRight()
    {
        var index = Cursor;

        while (index < Text.Length && !WordCharacters.IsWord(Text[index]))
        {
            index++;
        }

        while (index < Text.Length && WordCharacters.IsWord(Text[index]))
        {
            index++;
        }

        return this with { Cursor = index };
    }

    /// <summary>The start of the visual row the cursor is on.</summary>
    public TextBuffer RowStart(int capacity)
    {
        var rows = EditorLayout.Rows(Text, capacity, Tokens);

        return this with { Cursor = rows[EditorLayout.Locate(Text, rows, Cursor, Tokens).Row].Start };
    }

    /// <summary>The end of the visual row the cursor is on.</summary>
    public TextBuffer RowEnd(int capacity)
    {
        var rows = EditorLayout.Rows(Text, capacity, Tokens);
        var row = EditorLayout.Locate(Text, rows, Cursor, Tokens).Row;

        return this with { Cursor = EditorLayout.IndexAt(Text, rows, row, int.MaxValue, Tokens) };
    }

    private IReadOnlyList<AdfTokenSpan> TokensOf()
    {
        return Tokens ? AdfMarkdownTokens.Find(Text) : [];
    }

    // A token the cursor is at the end of or inside (a cursor moved by rows can land inside one): the key before it
    // acts on the whole token.
    private AdfTokenSpan? TokenEndingAt(int index)
    {
        return TokensOf().FirstOrDefault(span => span.Start < index && index <= span.End);
    }

    private AdfTokenSpan? TokenStartingAt(int index)
    {
        return TokensOf().FirstOrDefault(span => span.Start <= index && index < span.End);
    }

    private static string Truncated(string typed, int room)
    {
        var cut = room;

        // Never leave half of a surrogate pair.
        if (char.IsHighSurrogate(typed[cut - 1]))
        {
            cut--;
        }

        return typed[..cut];
    }
}
