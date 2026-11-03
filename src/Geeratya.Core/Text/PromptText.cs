using System.Globalization;

namespace Geeratya.Core.Text;

/// <summary>
/// The text of a prompt with a caret, and the editing every single-line field shares (palette, pickers, jump prompt,
/// form fields, help filter). The caret is stored as <paramref name="Back"/>, the UTF-16 units between the caret and
/// the end of the text, so a text that is set or appended to elsewhere keeps the caret at its end (0). Always at a
/// grapheme boundary: moves and deletes work by user-perceived character. Immutable and pure. A text with line
/// breaks (a paragraph field) has Home, End and the "to start/end" deletes work on the caret's line.
/// </summary>
/// <param name="Text">The text.</param>
/// <param name="Back">Units between the caret and the end of the text; clamped to the text when read.</param>
public readonly record struct PromptText(string Text, int Back)
{
    private const char LineBreak = '\n';

    /// <summary>The text with the caret at its end.</summary>
    public static PromptText Of(string text)
    {
        return new PromptText(text, 0);
    }

    /// <summary>The caret as an index into <see cref="Text"/>.</summary>
    public int Caret => Text.Length - Math.Clamp(Back, 0, Text.Length);

    /// <summary>Inserts <paramref name="typed"/> at the caret; the caret stays after it.</summary>
    public PromptText Insert(string typed)
    {
        if (typed.Length == 0)
        {
            return this;
        }

        var caret = Caret;

        return new PromptText($"{Text[..caret]}{typed}{Text[caret..]}", Text.Length - caret);
    }

    /// <summary>The text after one editing key; the same value when the key changes nothing.</summary>
    public PromptText Apply(PromptOperation operation)
    {
        var caret = Caret;

        return operation switch
        {
            PromptOperation.Left => MovedTo(PreviousStart(caret)),
            PromptOperation.Right => MovedTo(NextStart(caret)),
            PromptOperation.LineStart => MovedTo(LineStartOf(caret)),
            PromptOperation.LineEnd => MovedTo(LineEndOf(caret)),
            PromptOperation.WordLeft => MovedTo(WordStartBefore(caret)),
            PromptOperation.WordRight => MovedTo(WordEndAfter(caret)),
            PromptOperation.Backspace => Removed(PreviousStart(caret), caret),
            PromptOperation.Delete => Removed(caret, NextStart(caret)),
            PromptOperation.DeleteWordBefore => Removed(WordStartBefore(caret), caret),
            PromptOperation.DeleteWordAfter => Removed(caret, WordEndAfter(caret)),
            PromptOperation.DeleteToStart => Removed(LineStartOf(caret), caret),
            _ => Removed(caret, LineEndOf(caret)),
        };
    }

    private PromptText MovedTo(int caret)
    {
        return new PromptText(Text, Text.Length - caret);
    }

    // Removes [start, end); the caret ends at start.
    private PromptText Removed(int start, int end)
    {
        if (end <= start)
        {
            return this;
        }

        return new PromptText(Text.Remove(start, end - start), Text.Length - end);
    }

    private int PreviousStart(int caret)
    {
        var previous = 0;

        foreach (var start in StringInfo.ParseCombiningCharacters(Text))
        {
            if (start >= caret)
            {
                break;
            }

            previous = start;
        }

        return previous;
    }

    private int NextStart(int caret)
    {
        foreach (var start in StringInfo.ParseCombiningCharacters(Text))
        {
            if (start > caret)
            {
                return start;
            }
        }

        return Text.Length;
    }

    private int LineStartOf(int caret)
    {
        return caret == 0 ? 0 : Text.LastIndexOf(LineBreak, caret - 1) + 1;
    }

    private int LineEndOf(int caret)
    {
        var end = Text.IndexOf(LineBreak, caret);

        return end < 0 ? Text.Length : end;
    }

    // Spaces and punctuation are skipped first, then the word.
    private int WordStartBefore(int caret)
    {
        var index = caret;

        while (index > 0 && !WordCharacters.IsWord(Text[index - 1]))
        {
            index--;
        }

        while (index > 0 && WordCharacters.IsWord(Text[index - 1]))
        {
            index--;
        }

        return index;
    }

    private int WordEndAfter(int caret)
    {
        var index = caret;

        while (index < Text.Length && !WordCharacters.IsWord(Text[index]))
        {
            index++;
        }

        while (index < Text.Length && WordCharacters.IsWord(Text[index]))
        {
            index++;
        }

        return index;
    }
}
