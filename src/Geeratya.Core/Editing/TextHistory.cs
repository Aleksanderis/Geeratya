using System.Collections.Immutable;

namespace Geeratya.Core.Editing;

/// <summary>A text with its cursor and selection, as one undo or redo step restores it.</summary>
public sealed record TextSnapshot(string Text, int Cursor, int? Anchor);

/// <summary>
/// The undo and redo steps of a <see cref="TextBuffer"/> (the multi-line surfaces keep them; every other surface
/// never records, so its history stays <see cref="None"/>). Immutable. A run of single typed characters (or of
/// single deleted characters) is one step, cut at a word boundary, at a cursor move and at any other kind of edit;
/// a paste, a replaced selection and every bigger delete is a step of its own. At most <see cref="MaxSteps"/> steps are
/// kept. The history belongs to one text: when the text is not the one the last step ended with (something outside
/// the editing keys set it), undo and redo do nothing rather than restore an older text.
/// </summary>
public sealed record TextHistory
{
    /// <summary>The most undo steps kept; the oldest goes first.</summary>
    public const int MaxSteps = 100;

    private const int MaxJoinedUnits = 2;

    /// <summary>No steps.</summary>
    public static TextHistory None { get; } = new();

    /// <summary>The states to go back to, oldest first.</summary>
    public ImmutableArray<TextSnapshot> Undo { get; init; } = [];

    /// <summary>The states undo came from, most recently undone last.</summary>
    public ImmutableArray<TextSnapshot> Redo { get; init; } = [];

    /// <summary>The text and cursor after the last step, or null before any.</summary>
    public TextSnapshot? Tip { get; init; }

    private HistoryKind Last { get; init; }

    private enum HistoryKind
    {
        Other,
        Typing,
        Deleting,
    }

    /// <summary>The same steps, but the next edit starts a new one (the cursor moved).</summary>
    public TextHistory Broken()
    {
        return Last == HistoryKind.Other ? this : this with { Last = HistoryKind.Other };
    }

    /// <summary>Whether <paramref name="text"/> is the one the history belongs to.</summary>
    public bool Fits(string text)
    {
        return Tip is { } tip && string.Equals(tip.Text, text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The history after <paramref name="before"/> was edited into <paramref name="after"/> (their texts differ).
    /// </summary>
    public TextHistory Recorded(TextBuffer before, TextBuffer after)
    {
        var kind = KindOf(before, after);
        var tip = new TextSnapshot(after.Text, after.Cursor, null);
        var fresh = Fits(before.Text);

        if (fresh && kind != HistoryKind.Other && kind == Last && !Undo.IsEmpty && Joins(before, tip, kind))
        {
            return new TextHistory { Undo = Undo, Tip = tip, Last = kind };
        }

        var steps = fresh ? Undo : [];

        if (steps.Length >= MaxSteps)
        {
            steps = steps.RemoveAt(0);
        }

        var step = new TextSnapshot(before.Text, before.Cursor, before.Anchor);

        return new TextHistory { Undo = steps.Add(step), Tip = tip, Last = kind };
    }

    /// <summary><paramref name="current"/> one step back, or the same buffer when there is none.</summary>
    public TextBuffer Back(TextBuffer current)
    {
        if (Undo.IsEmpty || !Fits(current.Text))
        {
            return current;
        }

        var step = Undo[^1];
        var undone = new TextSnapshot(current.Text, current.Cursor, current.Anchor);
        var history = new TextHistory { Undo = Undo.RemoveAt(Undo.Length - 1), Redo = Redo.Add(undone), Tip = step };

        return Restored(current, step, history);
    }

    /// <summary><paramref name="current"/> one step forward again, or the same buffer when there is none.</summary>
    public TextBuffer Forward(TextBuffer current)
    {
        if (Redo.IsEmpty || !Fits(current.Text))
        {
            return current;
        }

        var step = Redo[^1];
        var left = new TextSnapshot(current.Text, current.Cursor, current.Anchor);
        var history = new TextHistory { Undo = Undo.Add(left), Redo = Redo.RemoveAt(Redo.Length - 1), Tip = step };

        return Restored(current, step, history);
    }

    private static TextBuffer Restored(TextBuffer current, TextSnapshot step, TextHistory history)
    {
        var anchor = step.Anchor is { } at && at != step.Cursor ? at : (int?)null;

        return current with { Text = step.Text, Cursor = step.Cursor, Anchor = anchor, History = history };
    }

    // A single typed character or a single deleted character with nothing selected; everything else is a step alone.
    private static HistoryKind KindOf(TextBuffer before, TextBuffer after)
    {
        if (before.HasSelection)
        {
            return HistoryKind.Other;
        }

        var prefix = 0;
        var shortest = Math.Min(before.Text.Length, after.Text.Length);

        while (prefix < shortest && before.Text[prefix] == after.Text[prefix])
        {
            prefix++;
        }

        var suffix = 0;

        while (suffix < shortest - prefix
            && before.Text[before.Text.Length - 1 - suffix] == after.Text[after.Text.Length - 1 - suffix])
        {
            suffix++;
        }

        var removed = before.Text.Length - prefix - suffix;
        var added = after.Text.Length - prefix - suffix;

        if (removed == 0 && added is > 0 and <= MaxJoinedUnits && !after.Text.AsSpan(prefix, added).Contains('\n'))
        {
            return HistoryKind.Typing;
        }

        return added == 0 && removed is > 0 and <= MaxJoinedUnits ? HistoryKind.Deleting : HistoryKind.Other;
    }

    // The edit continues the last step: it starts where that one ended, and a typed run stays within one word (or one
    // run of spaces).
    private bool Joins(TextBuffer before, TextSnapshot tip, HistoryKind kind)
    {
        if (Tip is not { } last || last.Cursor != before.Cursor)
        {
            return false;
        }

        if (kind == HistoryKind.Deleting)
        {
            return true;
        }

        if (before.Cursor == 0 || before.Cursor >= tip.Text.Length)
        {
            return false;
        }

        return char.IsWhiteSpace(tip.Text[before.Cursor]) == char.IsWhiteSpace(before.Text[before.Cursor - 1]);
    }
}
