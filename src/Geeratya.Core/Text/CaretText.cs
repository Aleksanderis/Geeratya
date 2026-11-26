using System.Globalization;
using Geeratya.Core.Editing;

namespace Geeratya.Core.Text;

/// <summary>
/// The one caret renderer of every text surface: the text with its caret drawn as a styled cell over the character it
/// sits on (a space after the last one), so moving it never shifts the text. Used for the one row of a single-line
/// field or prompt, the rows of a paragraph and the wrapped rows of the comment editor: the palette, pickers, jump
/// prompt, form fields, the profile form and the comment editor all draw their caret here, in
/// <see cref="TextRole.EditCaret"/>, and the typed text itself is always in <see cref="TypedRole"/>: no surface picks
/// a role of its own, so the same text and caret draw the same cells in every colour tier. Selected text (a buffer
/// with an <see cref="TextBuffer.Anchor"/>) is drawn in <see cref="SelectedRole"/>, the one selection look of every
/// surface that selects. <see cref="Marked"/> is the plain-text variant for the two surfaces that are one string (the
/// status bar and the Help title). Pure.
/// </summary>
public static class CaretText
{
    /// <summary>
    /// The role of typed text on every surface (the body text; the caret cell is <see cref="TextRole.EditCaret"/>).
    /// </summary>
    public const TextRole TypedRole = TextRole.Body;

    /// <summary>The role of selected text on every surface that selects (the caret cell keeps its own role).</summary>
    public const TextRole SelectedRole = TextRole.Range;

    /// <summary>
    /// The one row of a single-line input: the typed text, the caret as a block, the rest of the
    /// <paramref name="width"/> filled with blanks, so the row is as wide as its field.
    /// </summary>
    public static StyledLine Field(TextBuffer prompt, int width)
    {
        return FieldOf(PromptWindow.Of(prompt, width), width, MarkOf(prompt));
    }

    /// <summary>
    /// One row of a paragraph input (see <see cref="Field"/>); the row with the caret gets the caret block, the other
    /// rows are cut to the width.
    /// </summary>
    public static StyledLine FieldRow(TextBuffer prompt, string row, int index, int width)
    {
        var mark = RowMark(prompt, row, index);

        if (index != prompt.Line)
        {
            return FieldOf(new PromptWindow(CutText(row, width), string.Empty, string.Empty), width, mark);
        }

        return FieldOf(PromptWindow.Of(TextBuffer.FromBack(row, row.Length - prompt.Column), width), width, mark);
    }

    /// <summary>
    /// The text cut around its caret to fit, as spans in <see cref="TypedRole"/> with the caret as a styled cell over
    /// the character it sits on (a space after the last one), so moving it never shifts the text. No fill.
    /// </summary>
    public static IReadOnlyList<StyledSpan> Spans(TextBuffer prompt, int width)
    {
        return CutOf(PromptWindow.Of(prompt, width), width, MarkOf(prompt)).ToSpans(false, width);
    }

    /// <summary>A row without the caret, cut to the width (the end of a long row stays, behind an ellipsis).</summary>
    public static IReadOnlyList<StyledSpan> Cut(string row, int width, TextRole role)
    {
        return [new StyledSpan(CutText(row, width), role)];
    }

    /// <summary>
    /// One row of a paragraph as spans (see <see cref="Spans"/>); the other rows are cut to the width.
    /// </summary>
    public static IReadOnlyList<StyledSpan> ParagraphSpans(
        TextBuffer prompt,
        string row,
        int index,
        int width)
    {
        var mark = RowMark(prompt, row, index);

        if (index != prompt.Line)
        {
            var window = new PromptWindow(CutText(row, width), string.Empty, string.Empty);

            return mark is null ? Cut(row, width, TypedRole) : CutOf(window, width, mark).ToSpans(false, width);
        }

        var buffer = TextBuffer.FromBack(row, row.Length - prompt.Column);

        return CutOf(PromptWindow.Of(buffer, width), width, mark).ToSpans(false, width);
    }

    /// <summary>
    /// A row of already laid-out cells with the caret over one of them: <paramref name="cell"/> is the display of the
    /// character under the caret (a space when it is past the last one). For rows the caller wraps itself (the
    /// comment editor), where a mention or issue reference is one cell.
    /// </summary>
    public static IReadOnlyList<StyledSpan> Over(string before, string cell, string after)
    {
        return
        [
            new StyledSpan(before, TypedRole),
            new StyledSpan(cell, TextRole.EditCaret),
            new StyledSpan(after, TypedRole),
        ];
    }

    /// <summary>
    /// The cells of one wrapped row, the cell that starts at <paramref name="caret"/> in
    /// <see cref="TextRole.EditCaret"/> (a trailing space when the caret is past the last cell) and the cells whose
    /// start lies in <c>[selectionStart, selectionEnd)</c> in <see cref="SelectedRole"/>: for the comment editor, where
    /// a mention or issue reference is one cell. A caret of -1 draws no caret (a row of the text without it).
    /// </summary>
    public static IReadOnlyList<StyledSpan> OverCells(
        IReadOnlyList<EditorCell> cells,
        int caret,
        int selectionStart,
        int selectionEnd)
    {
        var spans = new List<StyledSpan>();
        var caretDrawn = false;

        foreach (var cell in cells)
        {
            var role = TypedRole;

            if (caret >= cell.Start && caret < cell.End)
            {
                role = TextRole.EditCaret;
                caretDrawn = true;
            }
            else if (cell.Start >= selectionStart && cell.Start < selectionEnd)
            {
                role = SelectedRole;
            }

            Append(spans, cell.Display, role);
        }

        if (caret >= 0 && !caretDrawn)
        {
            spans.Add(new StyledSpan(" ", TextRole.EditCaret));
        }

        return spans;
    }

    /// <summary>
    /// The text with a mark in place of the cell (<c>_</c> after the last character, <c>|</c> between two), cut to
    /// <paramref name="width"/>: only for a surface that is one plain string, where no cell can be styled.
    /// </summary>
    public static string Marked(TextBuffer prompt, int width)
    {
        var window = PromptWindow.Of(prompt, width);

        return $"{window.Before}{window.Caret}{window.After}";
    }

    // A row without the caret cut to the width: its end stays, behind an ellipsis.
    private static string CutText(string row, int width)
    {
        return PromptWindow.Of(TextBuffer.FromBack(row, 0), width).Before;
    }

    private static StyledLine FieldOf(PromptWindow window, int width, SelectionMark? mark)
    {
        return new StyledLine(CutOf(window, width, mark).ToSpans(true, width));
    }

    private static SelectionMark? MarkOf(TextBuffer prompt)
    {
        if (!prompt.HasSelection)
        {
            return null;
        }

        return new SelectionMark(prompt.Text, prompt.Cursor, prompt.SelectionStart, prompt.SelectionEnd);
    }

    // The part of a selection that lies in one row of a paragraph (its row text is what the mark describes).
    private static SelectionMark? RowMark(TextBuffer prompt, string row, int index)
    {
        if (!prompt.HasSelection)
        {
            return null;
        }

        var rowStart = 0;

        for (var skipped = 0; skipped < index; skipped++)
        {
            rowStart = prompt.Text.IndexOf('\n', rowStart) + 1;
        }

        var start = Math.Clamp(prompt.SelectionStart - rowStart, 0, row.Length);
        var end = Math.Clamp(prompt.SelectionEnd - rowStart, 0, row.Length);
        var caret = index == prompt.Line ? prompt.Column : row.Length;

        return start < end ? new SelectionMark(row, caret, start, end) : null;
    }

    private static void Append(List<StyledSpan> spans, string text, TextRole role)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (spans.Count > 0 && spans[^1].Role == role)
        {
            spans[^1] = spans[^1] with { Text = $"{spans[^1].Text}{text}" };

            return;
        }

        spans.Add(new StyledSpan(text, role));
    }

    // The window with the caret as a cell over the character it sits on, cut to the width.
    private static CaretCut CutOf(PromptWindow window, int width, SelectionMark? mark)
    {
        var before = window.Before;
        var after = window.After;
        var cell = " ";
        var consumed = 0;

        // An ellipsis for hidden text is no character to sit on: the cell is a blank and the ellipsis stays after it.
        if (window.Caret.Length > 0 && after.Length > 0 && !(window.TailCut && after == "…"))
        {
            cell = StringInfo.GetNextTextElement(after);
            consumed = cell.Length;
            after = after[cell.Length..];
        }

        var used = DisplayWidth.Of(before) + DisplayWidth.Of(cell) + DisplayWidth.Of(after);

        if (used > width && after.Length > 0)
        {
            after = DisplayText.Cut(after, Math.Max(0, DisplayWidth.Of(after) - (used - width)));
        }

        return new CaretCut(before, window.Caret.Length > 0 ? cell : null, after, mark, consumed);
    }

    // The text a selection is drawn over (the row or the whole text), the caret index and the selected range in it.
    private sealed record SelectionMark(string Text, int Caret, int Start, int End);

    private sealed record CaretCut(string Before, string? Cell, string After, SelectionMark? Mark, int CellLength)
    {
        public List<StyledSpan> ToSpans(bool fill, int width)
        {
            var spans = new List<StyledSpan>();

            AddBefore(spans);

            if (Cell is not null)
            {
                spans.Add(new StyledSpan(Cell, TextRole.EditCaret));
            }

            AddAfter(spans);

            var used = spans.Sum(static span => DisplayWidth.Of(span.Text));

            if (fill && used < width)
            {
                spans.Add(new StyledSpan(new string(' ', width - used), TypedRole));
            }

            return spans;
        }

        // The text before the caret shows its end, behind an ellipsis when it is cut.
        private void AddBefore(List<StyledSpan> spans)
        {
            if (Mark is null)
            {
                spans.Add(new StyledSpan(Before, TypedRole));

                return;
            }

            var real = Mark.Text[..Math.Min(Mark.Caret, Mark.Text.Length)];
            var head = Before == real || Before.Length == 0 ? 0 : 1;

            Append(spans, Before[..head], TypedRole);
            AddMarked(spans, Before[head..], real.Length - (Before.Length - head));
        }

        // The text after the caret cell shows its start, ending in an ellipsis when it is cut.
        private void AddAfter(List<StyledSpan> spans)
        {
            if (Mark is null)
            {
                spans.Add(new StyledSpan(After, TypedRole));

                return;
            }

            var from = Math.Min(Mark.Caret + CellLength, Mark.Text.Length);
            var shown = Mark.Text[from..].StartsWith(After, StringComparison.Ordinal)
                ? After.Length
                : Math.Max(0, After.Length - 1);

            AddMarked(spans, After[..shown], from);
            Append(spans, After[shown..], TypedRole);
        }

        // Text that starts at the index <paramref name="start" /> of the marked text, the selected part in its role.
        private void AddMarked(List<StyledSpan> spans, string text, int start)
        {
            var from = Math.Clamp(Mark!.Start - start, 0, text.Length);
            var to = Math.Clamp(Mark.End - start, from, text.Length);

            Append(spans, text[..from], TypedRole);
            Append(spans, text[from..to], SelectedRole);
            Append(spans, text[to..], TypedRole);
        }
    }
}
