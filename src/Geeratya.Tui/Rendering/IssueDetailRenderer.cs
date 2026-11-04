using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Platform.Terminal;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The body of the active tab while an issue is open on its stack (<c>docs/ux-screens.md</c>, "Issue detail"): one
/// frame whose top edge carries the title of the pane in view, around a window of the laid-out document (the breadcrumb
/// and the ruler are in the status bar, <see cref="DetailStatus"/>). The reducer lays the document out for the reported
/// width, so drawing only slices it; a document for another width (a size the host has not reported yet) is
/// rebuilt here.
/// </summary>
public sealed class IssueDetailRenderer : IBodyRenderer
{
    private const string CursorMark = ">";

    public bool AppliesTo(AppState state)
    {
        return state.Mode != InputMode.Help && ActiveTab(state) is { Detail.Count: > 0 };
    }

    public IReadOnlyList<StyledLine> Render(AppState state, BodyContext context, int width, int height)
    {
        var tab = ActiveTab(state)!;
        var top = tab.Detail[^1];
        var document = top.Document is { } laidOut && laidOut.Width == width
            ? laidOut
            : IssueDocumentBuilder.Build(top, width);
        var rows = Math.Max(0, height - DetailLayout.HeaderRows - DetailLayout.FooterRows);
        var first = Math.Clamp(top.Scroll, 0, Math.Max(0, document.Lines.Count - rows));
        var shown = Math.Max(0, Math.Min(rows, document.Lines.Count - first));
        var lines = new List<StyledLine>(height);
        var focused = document.Sections.Count == 0 ? -1 : document.SectionAt(top.Cursor);
        var block = document.BlockAt(top.Cursor);

        for (var index = first; index < first + shown; index++)
        {
            var drawn = Focus(document, index, focused);
            var ascii = context.Glyphs == GlyphSet.Ascii;
            var line = RowLine(drawn, index, top.Cursor, block, ascii);

            lines.Add(ascii ? AsciiBorders(line) : line);
        }

        if (!FrameGrid.CanFrame(width, height) || document.Sections.Count == 0)
        {
            return lines;
        }

        IReadOnlyList<StyledSpan>? right = null;
        var chips = new List<StyledSpan>();
        var inView = document.SectionAt(first);

        if (document.PaneTitles.Count > 0)
        {
            chips.AddRange(document.PaneTitles[inView]);
            right = inView < document.PaneRightTitles.Count ? document.PaneRightTitles[inView] : null;
        }

        // A document shorter than the pane is not padded: the closing edge sits directly under the last line.
        lines.Insert(0, PaneFrame.OuterEdge(width, height, true, chips, right, inView == focused));
        lines.Add(PaneFrame.OuterEdge(width, height, false, null, null, focused == document.Sections.Count - 1));

        return lines;
    }

    /// <summary>
    /// The line as drawn for the pane focus (<c>docs/ux-look.md</c>, section 4): the border cells of the focused pane's
    /// box (the shared edges above and below it included) in the focus role, and the chip of a shared edge filled when it
    /// titles the focused pane, dim otherwise.
    /// </summary>
    private static StyledLine Focus(DetailDocument document, int index, int focused)
    {
        var line = document.Lines[index];
        var section = document.SectionAt(index);
        var isEdge = document.Sections.Contains(index + 1);
        var accent = focused == section || (isEdge && focused == section + 1);
        var spans = line.Spans.ToList();

        if (isEdge && spans.Count > 2 && spans[0].Role == TextRole.Border)
        {
            var chip = ChipStyle.Dim(spans.GetRange(1, spans.Count - 2));

            if (focused == section + 1)
            {
                chip = ChipStyle.Focused(chip);
            }

            spans = [spans[0], .. chip, spans[^1]];
        }

        if (accent)
        {
            spans = [.. spans.Select(static span => span.Role == TextRole.Border ? FocusBorder(span) : span)];
        }

        return new StyledLine(spans);
    }

    private static StyledSpan FocusBorder(StyledSpan span)
    {
        return span with { Role = TextRole.FrameFocus };
    }

    /// <summary>
    /// Marks the cursor row: the padding cell after the border (the border is always the first span) becomes <c>&gt;</c> in
    /// the selected role; a line without one (frames are dropped on a very narrow screen) is shown selected as a whole.
    /// </summary>
    private static StyledLine CursorLine(StyledLine line)
    {
        if (line.Spans.Count > 0
            && line.Spans[0] is { Role: TextRole.Border or TextRole.FrameFocus, Text.Length: > 1 } first
            && IsBorder(first.Text[0]))
        {
            // The marker goes into the padding column inside the border, never over it.
            var marked = new List<StyledSpan>(line.Spans.Count + 2)
            {
                new(first.Text[..1], first.Role),
                new(CursorMark, TextRole.Selected),
            };

            if (first.Text.Length > 2)
            {
                marked.Add(first with { Text = first.Text[2..] });
            }

            marked.AddRange(line.Spans.Skip(1));

            return new StyledLine(marked);
        }

        return new StyledLine(line.Spans.Select(static span => span with { Role = TextRole.Selected }).ToArray());
    }

    private static StyledLine RowLine(StyledLine drawn, int index, int cursor, LineBlock? block, bool ascii)
    {
        if (block is not null && index >= block.Start && index < block.End)
        {
            return FieldLine(drawn, index == block.Start, ascii);
        }

        return index == cursor ? CursorLine(drawn) : drawn;
    }

    /// <summary>
    /// A row of the focused field: the whole row between the borders in the selected role (label, value and padding), so
    /// a wrapped value reads as one bar; the first row also carries the <c>&gt;</c> mark, so focus is not colour alone.
    /// </summary>
    private static StyledLine FieldLine(StyledLine line, bool first, bool ascii)
    {
        var marked = first ? CursorLine(line) : line;
        var spans = marked.Spans.Select(span => IsBorderSpan(span) ? span : Focused(span, ascii));

        return new StyledLine(spans.ToArray());
    }

    private static StyledSpan Focused(StyledSpan span, bool ascii)
    {
        var text = ascii && span.Role == TextRole.Editable ? span.Text.Replace('▾', 'v') : span.Text;

        return span with { Text = text, Role = TextRole.Selected };
    }

    /// <summary>Swaps the panel border characters for ASCII, one cell for one cell.</summary>
    private static StyledLine AsciiBorders(StyledLine line)
    {
        if (!line.Spans.Any(static span => (IsBorderSpan(span) && span.Text.Any(IsBorder))
            || span.Role == TextRole.Editable))
        {
            return line;
        }

        return new StyledLine(line.Spans.Select(static span => span.Role switch
        {
            TextRole.Border or TextRole.FrameFocus => span with { Text = SwapBorders(span.Text) },
            TextRole.Editable => span with { Text = span.Text.Replace('▾', 'v') },
            _ => span,
        }).ToArray());
    }

    private static bool IsBorderSpan(StyledSpan span)
    {
        return span.Role is TextRole.Border or TextRole.FrameFocus;
    }

    private static string SwapBorders(string text)
    {
        return string.Concat(text.Select(static character => IsBorder(character) ? AsciiOf(character) : character));
    }

    private static bool IsBorder(char character)
    {
        return character is '╭' or '╮' or '╰' or '╯' or '─' or '│';
    }

    private static char AsciiOf(char character)
    {
        return character switch
        {
            '─' => '-',
            '│' => '|',
            _ => '+',
        };
    }

    private static TabState? ActiveTab(AppState state)
    {
        return state.Session.Active is { } id ? state.Session.Tabs.FirstOrDefault(tab => tab.Id == id) : null;
    }
}
