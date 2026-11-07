using System.Globalization;

namespace Geeratya.Core.Text;

/// <summary>
/// The rows of a text field with its caret drawn in them: the one row of a single-line field and the rows of a
/// paragraph field. Shared by the form box, the create panes and the in-place editors of the detail. Pure.
/// </summary>
public static class CaretRows
{
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

    /// <summary>
    /// The text cut around its caret to fit, as spans in <paramref name="role"/> with the caret as a styled cell over
    /// the character it sits on (a space after the last one), so moving it never shifts the text. No fill.
    /// </summary>
    public static IReadOnlyList<StyledSpan> Spans(PromptText prompt, int width, TextRole role)
    {
        return CutOf(PromptWindow.Of(prompt, width), width).ToSpans(role, false, width);
    }

    /// <summary>
    /// One row of a paragraph as spans (see <see cref="Spans"/>); the other rows are cut to the width.
    /// </summary>
    public static IReadOnlyList<StyledSpan> ParagraphSpans(
        PromptText prompt,
        string row,
        int index,
        int caretRow,
        int width,
        TextRole role)
    {
        if (index != caretRow)
        {
            return [new StyledSpan(PromptWindow.Of(new PromptText(row, 0), width).Before, role)];
        }

        var lineStart = prompt.Text[..prompt.Caret].LastIndexOf('\n') + 1;
        var column = prompt.Caret - lineStart;

        return Spans(new PromptText(row, row.Length - column), width, role);
    }

    private static StyledLine FieldOf(PromptWindow window, int width)
    {
        return new StyledLine(CutOf(window, width).ToSpans(TextRole.EditField, true, width));
    }

    // The window with the caret as a cell over the character it sits on, cut to the width.
    private static CaretCut CutOf(PromptWindow window, int width)
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
        }

        return new CaretCut(before, window.Caret.Length > 0 ? cell : null, after);
    }

    private sealed record CaretCut(string Before, string? Cell, string After)
    {
        public List<StyledSpan> ToSpans(TextRole role, bool fill, int width)
        {
            var spans = new List<StyledSpan> { new(Before, role) };

            if (Cell is not null)
            {
                spans.Add(new StyledSpan(Cell, TextRole.EditCaret));
            }

            spans.Add(new StyledSpan(After, role));

            var used = spans.Sum(static span => DisplayWidth.Of(span.Text));

            if (fill && used < width)
            {
                spans.Add(new StyledSpan(new string(' ', width - used), role));
            }

            return spans;
        }
    }
}
