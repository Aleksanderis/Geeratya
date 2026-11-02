using System.Globalization;
using Geeratya.Jira.Adf;

namespace Geeratya.Core.Editing;

/// <summary>
/// A text with a cursor (a UTF-16 index at the start of a character) and the edits an editor needs. Immutable: every
/// edit returns a new buffer. The cursor moves by character (grapheme), never inside a surrogate pair or a combining
/// sequence. Up, Down, Home and End move by visual row, so they need the row capacity the text is laid out with.
/// </summary>
/// <param name="Text">The text; lines are separated by <c>\n</c>.</param>
/// <param name="Cursor">The index of the character the cursor is before; the text length at the end.</param>
public sealed record TextBuffer(string Text, int Cursor)
{
    /// <summary>The most characters a buffer takes (Jira refuses a comment above 32 767).</summary>
    public const int MaxLength = 30000;

    public static TextBuffer Empty { get; } = new(string.Empty, 0);

    /// <summary>A buffer with <paramref name="text"/> and the cursor at its end.</summary>
    public static TextBuffer Of(string text)
    {
        return new TextBuffer(text, text.Length);
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

        return new TextBuffer(Text.Insert(Cursor, accepted), Cursor + accepted.Length);
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
            return new TextBuffer(Text.Remove(token.Start, token.End - token.Start), token.Start);
        }

        var start = EditorLayout.PreviousStart(Text, Cursor);

        return new TextBuffer(Text.Remove(start, Cursor - start), start);
    }

    /// <summary>The text with <c>[start, end)</c> replaced by <paramref name="replacement"/>, the cursor after it.</summary>
    public TextBuffer Replace(int start, int end, string replacement)
    {
        var text = $"{Text[..start]}{replacement}{Text[end..]}";

        return text.Length > MaxLength ? this : new TextBuffer(text, start + replacement.Length);
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
            return new TextBuffer(Text.Remove(token.Start, token.End - token.Start), token.Start);
        }

        return this with { Text = Text.Remove(Cursor, EditorLayout.NextStart(Text, Cursor) - Cursor) };
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
    /// <paramref name="direction"/> visual rows up (negative) or down, stopping at the first or last row, keeping
    /// the display column where the row is long enough.
    /// </summary>
    public TextBuffer Vertical(int direction, int capacity)
    {
        var rows = EditorLayout.Rows(Text, capacity);
        var position = EditorLayout.Locate(Text, rows, Cursor);
        var target = Math.Clamp(position.Row + direction, 0, rows.Count - 1);

        if (target == position.Row)
        {
            return this;
        }

        return this with { Cursor = EditorLayout.IndexAt(Text, rows, target, position.Column) };
    }

    /// <summary>The start of the word before the cursor (spaces and punctuation skipped first), or the text start.</summary>
    public TextBuffer WordLeft()
    {
        var index = Cursor;

        while (index > 0 && !IsWordCharacter(Text[index - 1]))
        {
            index--;
        }

        while (index > 0 && IsWordCharacter(Text[index - 1]))
        {
            index--;
        }

        return this with { Cursor = index };
    }

    /// <summary>The end of the word at or after the cursor (spaces and punctuation skipped first), or the text end.</summary>
    public TextBuffer WordRight()
    {
        var index = Cursor;

        while (index < Text.Length && !IsWordCharacter(Text[index]))
        {
            index++;
        }

        while (index < Text.Length && IsWordCharacter(Text[index]))
        {
            index++;
        }

        return this with { Cursor = index };
    }

    /// <summary>The start of the visual row the cursor is on.</summary>
    public TextBuffer RowStart(int capacity)
    {
        var rows = EditorLayout.Rows(Text, capacity);

        return this with { Cursor = rows[EditorLayout.Locate(Text, rows, Cursor).Row].Start };
    }

    /// <summary>The end of the visual row the cursor is on.</summary>
    public TextBuffer RowEnd(int capacity)
    {
        var rows = EditorLayout.Rows(Text, capacity);
        var row = EditorLayout.Locate(Text, rows, Cursor).Row;

        return this with { Cursor = EditorLayout.IndexAt(Text, rows, row, int.MaxValue) };
    }

    // A token the cursor is at the end of or inside (a cursor moved by rows can land inside one): the key before it
    // acts on the whole token.
    private AdfTokenSpan? TokenEndingAt(int index)
    {
        return AdfMarkdownTokens.Find(Text).FirstOrDefault(span => span.Start < index && index <= span.End);
    }

    private AdfTokenSpan? TokenStartingAt(int index)
    {
        return AdfMarkdownTokens.Find(Text).FirstOrDefault(span => span.Start <= index && index < span.End);
    }

    private static bool IsWordCharacter(char value)
    {
        if (char.IsLetterOrDigit(value) || value == '_' || char.IsSurrogate(value))
        {
            return true;
        }

        // Marks and joiners (a zero-width joiner is a format character) keep a grapheme cluster in one piece.
        return CharUnicodeInfo.GetUnicodeCategory(value)
            is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark
            or UnicodeCategory.Format;
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
