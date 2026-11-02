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

        var start = EditorLayout.PreviousStart(Text, Cursor);

        return new TextBuffer(Text.Remove(start, Cursor - start), start);
    }

    /// <summary>Removes the character under the cursor.</summary>
    public TextBuffer Delete()
    {
        if (Cursor >= Text.Length)
        {
            return this;
        }

        return this with { Text = Text.Remove(Cursor, EditorLayout.NextStart(Text, Cursor) - Cursor) };
    }

    public TextBuffer Left()
    {
        return this with { Cursor = EditorLayout.PreviousStart(Text, Cursor) };
    }

    public TextBuffer Right()
    {
        return this with { Cursor = EditorLayout.NextStart(Text, Cursor) };
    }

    /// <summary>One visual row up or down, keeping the display column where the row is long enough.</summary>
    public TextBuffer Vertical(int direction, int capacity)
    {
        var rows = EditorLayout.Rows(Text, capacity);
        var position = EditorLayout.Locate(Text, rows, Cursor);
        var target = position.Row + direction;

        if (target < 0 || target >= rows.Count)
        {
            return this;
        }

        return this with { Cursor = EditorLayout.IndexAt(Text, rows, target, position.Column) };
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
