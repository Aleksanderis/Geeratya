using System.Globalization;

namespace Geeratya.Core.Text;

/// <summary>
/// The rows of a text field with its caret drawn in them: the one row of a single-line field and the rows of a
/// paragraph field. Shared by the form box, the create panes and the in-place editors of the detail. Pure.
/// </summary>
public static class CaretRows
{
    /// <summary>
    /// The text cut around its caret to fit, with the caret mark drawn in it (<c>_</c> at the end, <c>|</c> inside).
    /// </summary>
    public static string Caretted(PromptText prompt, int width)
    {
        var window = PromptWindow.Of(prompt, width);

        return $"{window.Before}{window.Caret}{window.After}";
    }

    /// <summary>
    /// One row of a paragraph: the row with the caret gets the caret mark, the other rows are cut to the width.
    /// </summary>
    /// <param name="prompt">The whole text and its caret.</param>
    /// <param name="row">The text of the row.</param>
    /// <param name="index">The row number.</param>
    /// <param name="caretRow">The row number the caret is on, -1 for none.</param>
    /// <param name="width">The columns of the row.</param>
    public static string ParagraphRow(PromptText prompt, string row, int index, int caretRow, int width)
    {
        if (index != caretRow)
        {
            return PromptWindow.Of(new PromptText(row, 0), width).Before;
        }

        var lineStart = prompt.Text[..prompt.Caret].LastIndexOf('\n') + 1;
        var column = prompt.Caret - lineStart;

        return Caretted(new PromptText(row, row.Length - column), width);
    }

    /// <summary>
    /// The one row of a single-line input drawn as a text box: the text on the field fill, the caret as a block, the
    /// rest of the <paramref name="width"/> filled, so the row reads as an input and not as text.
    /// </summary>
    public static StyledLine Field(PromptText prompt, int width)
    {
        return FieldOf(PromptWindow.Of(prompt, width), width);
    }

    /// <summary>
    /// One row of a paragraph input drawn as a text box (see <see cref="Field"/>); the row with the caret gets the
    /// caret block, the other rows are cut to the width.
    /// </summary>
    public static StyledLine FieldRow(PromptText prompt, string row, int index, int caretRow, int width)
    {
        if (index != caretRow)
        {
            return FieldOf(new PromptWindow(PromptWindow.Of(new PromptText(row, 0), width).Before, string.Empty, string.Empty), width);
        }

        var lineStart = prompt.Text[..prompt.Caret].LastIndexOf('\n') + 1;
        var column = prompt.Caret - lineStart;

        return Field(new PromptText(row, row.Length - column), width);
    }

    // The window as spans: the caret is a styled cell over the character it sits on (a space after the last one),
    // so moving it never shifts the text.
    private static StyledLine FieldOf(PromptWindow window, int width)
    {
        var before = window.Before;
        var after = window.After;
        var cell = " ";

        if (window.Caret.Length > 0 && after.Length > 0)
        {
            cell = StringInfo.GetNextTextElement(after);
            after = after[cell.Length..];
        }

        var used = DisplayWidth.Of(before) + DisplayWidth.Of(cell) + DisplayWidth.Of(after);

        if (used > width && after.Length > 0)
        {
            after = DisplayText.Cut(after, Math.Max(0, DisplayWidth.Of(after) - (used - width)));
            used = DisplayWidth.Of(before) + DisplayWidth.Of(cell) + DisplayWidth.Of(after);
        }

        var spans = new List<StyledSpan> { new(before, TextRole.EditField) };

        if (window.Caret.Length > 0)
        {
            spans.Add(new StyledSpan(cell, TextRole.EditCaret));
        }

        spans.Add(new StyledSpan(after, TextRole.EditField));

        if (used < width)
        {
            spans.Add(new StyledSpan(new string(' ', width - used), TextRole.EditField));
        }

        return new StyledLine(spans);
    }
}
