using System.Globalization;
using Geeratya.Core.Editing;

namespace Geeratya.Core.Text;

/// <summary>
/// The one caret renderer of every text surface: the text with its caret drawn as a styled cell over the character it
/// sits on (a space after the last one), so moving it never shifts the text. Used for the one row of a single-line
/// field or prompt, the rows of a paragraph and the wrapped rows of the comment editor: the palette, pickers, jump
/// prompt, form fields, the profile form and the comment editor all draw their caret here, in
/// <see cref="TextRole.EditCaret"/>, and the typed text itself is always in <see cref="TypedRole"/>: no surface picks
/// a role of its own, so the same text and caret draw the same cells in every colour tier. <see cref="Marked"/> is the plain-text variant for the two surfaces that are one
/// string (the status bar and the Help title). Pure.
/// </summary>
public static class CaretText
{
    /// <summary>The role of typed text on every surface (the body text; the caret cell is <see cref="TextRole.EditCaret"/>).</summary>
    public const TextRole TypedRole = TextRole.Body;

    /// <summary>
    /// The one row of a single-line input: the typed text, the caret as a block, the rest of the
    /// <paramref name="width"/> filled with blanks, so the row is as wide as its field.
    /// </summary>
    public static StyledLine Field(TextBuffer prompt, int width)
    {
        return FieldOf(PromptWindow.Of(prompt, width), width);
    }

    /// <summary>
    /// One row of a paragraph input (see <see cref="Field"/>); the row with the caret gets the caret block, the other
    /// rows are cut to the width.
    /// </summary>
    public static StyledLine FieldRow(TextBuffer prompt, string row, int index, int width)
    {
        if (index != prompt.Line)
        {
            return FieldOf(new PromptWindow(CutText(row, width), string.Empty, string.Empty), width);
        }

        return Field(TextBuffer.FromBack(row, row.Length - prompt.Column), width);
    }

    /// <summary>
    /// The text cut around its caret to fit, as spans in <see cref="TypedRole"/> with the caret as a styled cell over
    /// the character it sits on (a space after the last one), so moving it never shifts the text. No fill.
    /// </summary>
    public static IReadOnlyList<StyledSpan> Spans(TextBuffer prompt, int width)
    {
        return CutOf(PromptWindow.Of(prompt, width), width).ToSpans(false, width);
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
        if (index != prompt.Line)
        {
            return Cut(row, width, TypedRole);
        }

        return Spans(TextBuffer.FromBack(row, row.Length - prompt.Column), width);
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

    private static StyledLine FieldOf(PromptWindow window, int width)
    {
        return new StyledLine(CutOf(window, width).ToSpans(true, width));
    }

    // The window with the caret as a cell over the character it sits on, cut to the width.
    private static CaretCut CutOf(PromptWindow window, int width)
    {
        var before = window.Before;
        var after = window.After;
        var cell = " ";

        // An ellipsis for hidden text is no character to sit on: the cell is a blank and the ellipsis stays after it.
        if (window.Caret.Length > 0 && after.Length > 0 && !(window.TailCut && after == "…"))
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
        public List<StyledSpan> ToSpans(bool fill, int width)
        {
            var spans = new List<StyledSpan> { new(Before, TypedRole) };

            if (Cell is not null)
            {
                spans.Add(new StyledSpan(Cell, TextRole.EditCaret));
            }

            spans.Add(new StyledSpan(After, TypedRole));

            var used = spans.Sum(static span => DisplayWidth.Of(span.Text));

            if (fill && used < width)
            {
                spans.Add(new StyledSpan(new string(' ', width - used), TypedRole));
            }

            return spans;
        }
    }
}
