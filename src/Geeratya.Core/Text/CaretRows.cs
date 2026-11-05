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

    // The window as spans: the mark keeps its character (so the caret shows in plain text too) inside a filled block.
    private static StyledLine FieldOf(PromptWindow window, int width)
    {
        var mark = window.Caret;
        var used = DisplayWidth.Of(window.Before) + DisplayWidth.Of(mark) + DisplayWidth.Of(window.After);
        var spans = new List<StyledSpan> { new(window.Before, TextRole.EditField) };

        if (mark.Length > 0)
        {
            spans.Add(new StyledSpan(mark, TextRole.EditCaret));
        }

        spans.Add(new StyledSpan(window.After, TextRole.EditField));

        if (used < width)
        {
            spans.Add(new StyledSpan(new string(' ', width - used), TextRole.EditField));
        }

        return new StyledLine(spans);
    }
}
