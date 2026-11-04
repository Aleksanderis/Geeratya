namespace Geeratya.Core.Text;

/// <summary>
/// The rows of a text field with its caret drawn in them: the one row of a single-line field and the rows of a
/// paragraph field. Shared by the form box, the create panes and the in-place editors of the detail. Pure.
/// </summary>
public static class CaretRows
{
    /// <summary>The text cut around its caret to fit, with the caret mark drawn in it (<c>_</c> at the end, <c>|</c> inside).</summary>
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
}
