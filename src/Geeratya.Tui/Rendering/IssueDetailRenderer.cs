using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Panes;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Platform.Terminal;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The body of the active tab while an issue is open on its stack (<c>docs/ux-screens.md</c>, "Pane layout for the
/// issue detail"): a window of the laid-out document, a stack of panes that each carry their own frame (the breadcrumb
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
        var rows = Math.Max(0, height - DetailLayout.HeaderRows - DetailLayout.FooterRows);
        var document = top.Document is { } laidOut && laidOut.Width == width
            ? laidOut
            : IssueDocumentBuilder.Build(top, width, rows);
        var first = Math.Clamp(top.Scroll, 0, Math.Max(0, document.Lines.Count - rows));
        var shown = Math.Max(0, Math.Min(rows, document.Lines.Count - first));
        var lines = new List<StyledLine>(height);
        var focused = document.Sections.Count == 0 ? -1 : document.SectionAt(top.Cursor);
        var block = document.BlockAt(top.Cursor);
        var ascii = context.Glyphs == GlyphSet.Ascii;

        for (var index = first; index < first + shown; index++)
        {
            var drawn = Pane(document, index, focused);
            var line = RowLine(drawn, index, top.Cursor, block, ascii);

            lines.Add(ascii ? AsciiBorders(line) : line);
        }

        return lines;
    }

    /// <summary>
    /// The line as drawn for the pane focus (<c>docs/ux-look.md</c>, section 4): the border cells of the focused pane's
    /// box in the focus role, its title chip filled, every other chip dim; the scrollbar of a cut pane in the right
    /// padding column.
    /// </summary>
    private static StyledLine Pane(DetailDocument document, int index, int focused)
    {
        var line = document.Lines[index];
        var pane = document.SectionAt(index);
        var spans = Scrollbar(document, pane, index, [.. line.Spans]);

        if (index == document.PaneStarts[pane] && spans.Count > 1 && spans[0].Role == TextRole.Border)
        {
            spans = Chips(spans, pane == focused);
        }

        if (pane == focused)
        {
            spans = [.. spans.Select(static span => span.Role == TextRole.Border ? FocusBorder(span) : span)];
        }

        return new StyledLine(spans);
    }

    /// <summary>
    /// The chips of a top edge (the runs between border spans) restyled: the first one for the focus state.
    /// </summary>
    private static List<StyledSpan> Chips(List<StyledSpan> spans, bool focused)
    {
        var result = new List<StyledSpan>(spans.Count);
        var run = new List<StyledSpan>();
        var chip = 0;

        foreach (var span in spans.Append(new StyledSpan(string.Empty, TextRole.Border)))
        {
            if (span.Role != TextRole.Border)
            {
                run.Add(span);

                continue;
            }

            if (run.Count > 0)
            {
                result.AddRange(chip == 0 && focused ? ChipStyle.Focused(run) : ChipStyle.Dim(run));
                run.Clear();
                chip++;
            }

            if (span.Text.Length > 0)
            {
                result.Add(span);
            }
        }

        return result;
    }

    /// <summary>
    /// The right padding cell of a content row replaced by the scrollbar cell of its row, when the pane scrolls.
    /// </summary>
    private static List<StyledSpan> Scrollbar(DetailDocument document, int pane, int index, List<StyledSpan> spans)
    {
        if (document.PaneScrolls[pane] is not { } scroll || spans.Count < 2)
        {
            return spans;
        }

        var row = index - document.Sections[pane];
        var cells = ScrollBar.Cells(scroll, scroll.Viewport);

        if (row < 0 || row >= cells.Count || spans[^1] is not { Role: TextRole.Border, Text: [' ', _] } edge)
        {
            return spans;
        }

        spans[^1] = new StyledSpan(GlyphOf(cells[row]), TextRole.Border);
        spans.Add(edge with { Text = edge.Text[1..] });

        return spans;
    }

    private static string GlyphOf(ScrollCell cell)
    {
        return cell switch
        {
            ScrollCell.Up => "▲",
            ScrollCell.Thumb => "█",
            ScrollCell.Track => "░",
            _ => "▼",
        };
    }

    private static StyledSpan FocusBorder(StyledSpan span)
    {
        return span with { Role = TextRole.FrameFocus };
    }

    /// <summary>
    /// Marks the cursor row: the padding cell after the border (the border is always the first span) becomes
    /// <c>&gt;</c> in
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

        return index == cursor ? CursorLine(Untinted(drawn)) : drawn;
    }

    /// <summary>A comment's header bar loses its tint under the cursor: the selection tint takes its place.</summary>
    private static StyledLine Untinted(StyledLine line)
    {
        if (!line.Spans.Any(static span => span.Role == TextRole.ValueBlock))
        {
            return line;
        }

        return new StyledLine([.. line.Spans.Select(Untinted)]);
    }

    private static StyledSpan Untinted(StyledSpan span)
    {
        return span.Role == TextRole.ValueBlock ? span with { Role = TextRole.Selected } : span;
    }

    /// <summary>
    /// A row of the focused field: the whole row between the borders in the selected role (label, value and padding),
    /// so
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
        return character is '╭' or '╮' or '╰' or '╯' or '─' or '│' or '▲' or '█' or '░' or '▼';
    }

    private static char AsciiOf(char character)
    {
        return character switch
        {
            '─' => '-',
            '│' => '|',
            '▲' => '^',
            '█' => '#',
            '░' => '.',
            '▼' => 'v',
            _ => '+',
        };
    }

    private static TabState? ActiveTab(AppState state)
    {
        return state.Session.Active is { } id ? state.Session.Tabs.FirstOrDefault(tab => tab.Id == id) : null;
    }
}
