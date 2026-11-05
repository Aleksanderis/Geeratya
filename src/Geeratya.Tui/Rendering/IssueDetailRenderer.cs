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
    private const string StatusDot = "●";

    public bool AppliesTo(AppState state)
    {
        return state.Mode != InputMode.Help && ActiveTab(state) is { Detail.Count: > 0 };
    }

    public IReadOnlyList<StyledLine> Render(AppState state, BodyContext context, int width, int height)
    {
        var tab = ActiveTab(state)!;
        var top = tab.Detail[^1];
        var stripRows = top.Zoomed is null ? 0 : DetailZoom.StripRows;
        var rows = Math.Max(0, height - DetailLayout.HeaderRows - DetailLayout.FooterRows - stripRows);
        var editing = PaneEdit.PaneOf(tab.Form, top.Key);
        var document = top.Document is { } laidOut && laidOut.Width == width && editing is null
            ? laidOut
            : IssueDocumentBuilder.Build(top, width, rows, null, tab.Form);
        var first = Math.Clamp(top.Scroll, 0, Math.Max(0, document.Lines.Count - rows));
        var side = document.Side;
        var onSide = side is not null && top.SideCursor is not null && editing is null;
        var focused = document.Sections.Count == 0 || onSide ? -1 : document.SectionAt(top.Cursor);
        var block = onSide ? null : document.BlockAt(top.Cursor);
        var cursor = onSide ? -1 : top.Cursor;

        if (editing is not null)
        {
            // The edited pane keeps the focus, whatever its rows did to the line numbers; a screen scrolled past it
            // shows it from its top edge.
            focused = IndexOf(document.SectionIds, editing);
            block = null;
            cursor = -1;
            first = focused >= 0 ? Math.Min(first, document.PaneStarts[focused]) : first;
        }

        var shown = side is null ? Math.Max(0, Math.Min(rows, document.Lines.Count - first)) : rows;
        var lines = new List<StyledLine>(height);

        if (document.IsZoomed)
        {
            lines.Add(SectionStrip(document, top.Zoomed, width));
        }

        var ascii = context.Glyphs == GlyphSet.Ascii;
        var sideLines = side is null ? [] : SideColumn(side, onSide ? top.SideCursor : null, rows, ascii);
        var mainWidth = side is null ? width : DetailLayout.MainWidth(width);
        var range = editing is null ? DetailText.SelectedLines(top) : null;

        for (var row = 0; row < shown; row++)
        {
            var index = first + row;
            var line = index < document.Lines.Count
                ? InRange(RowLine(Pane(document, index, focused), index, cursor, block, document, ascii), index, range, cursor)
                : StyledLine.Empty;

            if (side is not null)
            {
                line = Joined(line, mainWidth, row < sideLines.Count ? sideLines[row] : StyledLine.Empty);
            }

            lines.Add(ascii ? AsciiBorders(line) : line);
        }

        return lines;
    }

    /// <summary>
    /// A line of the Visual selection (<c>v</c>) in the range tint between the borders; the cursor line, the moving
    /// end, keeps the selected tint.
    /// </summary>
    private static StyledLine InRange(StyledLine drawn, int index, LineBlock? range, int cursor)
    {
        if (range is null || index < range.Start || index >= range.End || index == cursor)
        {
            return drawn;
        }

        var spans = drawn.Spans.Select(static span => IsBorderSpan(span) ? span : span with { Role = TextRole.Range });

        return new StyledLine(spans.ToArray());
    }

    private static int IndexOf(IReadOnlyList<string> ids, string id)
    {
        for (var index = 0; index < ids.Count; index++)
        {
            if (ids[index] == id)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// The section strip of a zoomed pane: every section by its title, the current one as the filled accent chip, the
    /// others muted. A strip wider than the screen drops sections from the left until the current one is in view, so
    /// <c>Ctrl-h</c>/<c>Ctrl-l</c> always show where they went.
    /// </summary>
    private static StyledLine SectionStrip(DetailDocument document, string? current, int width)
    {
        var spans = new List<StyledSpan>();

        foreach (var chip in DetailStrip.Chips(document, current, width))
        {
            spans.Add(new StyledSpan(chip.Text, chip.Id == current ? TextRole.ChipFocus : TextRole.Muted));
            spans.Add(new StyledSpan(" ", TextRole.Body));
        }

        return StyledLines.Fit(new StyledLine(spans), width);
    }

    /// <summary>The main column line padded to its width, a gap, then the sidebar line.</summary>
    private static StyledLine Joined(StyledLine main, int mainWidth, StyledLine side)
    {
        var spans = new List<StyledSpan>(main.Spans);
        var padding = mainWidth + DetailLayout.SidebarGap - TextWidth.Of(main.PlainText);

        if (padding > 0)
        {
            spans.Add(new StyledSpan(new string(' ', padding), TextRole.Body));
        }

        spans.AddRange(side.Spans);

        return new StyledLine(spans);
    }

    /// <summary>
    /// The Details sidebar as <paramref name="rows"/> lines: the top edge, the content window that keeps the cursor
    /// field visible, blank framed rows down to the bottom edge, so the frame is as tall as the overlay.
    /// </summary>
    private static List<StyledLine> SideColumn(DetailDocument side, int? cursor, int rows, bool ascii)
    {
        var focused = cursor is not null;
        var first = SideView.FirstLine(side, cursor, rows);
        var block = cursor is { } at ? side.BlockAt(at) : null;
        var lines = new List<StyledLine>(rows);

        for (var row = 0; row < rows; row++)
        {
            var index = first + row;

            if (row == 0)
            {
                lines.Add(SideEdge(side.Lines[0], focused, true));
            }
            else if (row == rows - 1)
            {
                lines.Add(SideEdge(side.Lines[^1], focused, false));
            }
            else if (index < side.Lines.Count - 1)
            {
                var line = SideEdge(side.Lines[index], focused, false);
                var inBlock = block is not null && index > block.Start && index < block.End;

                lines.Add(inBlock ? FieldBlock.Focus(line, ascii) : line);
            }
            else
            {
                lines.Add(BlankFramed(side.Width, focused));
            }
        }

        return lines;
    }

    private static StyledLine SideEdge(StyledLine line, bool focused, bool top)
    {
        var spans = top ? Chips([.. line.Spans], focused) : [.. line.Spans];

        if (focused)
        {
            spans = [.. spans.Select(static span => span.Role == TextRole.Border ? FocusBorder(span) : span)];
        }

        return new StyledLine(spans);
    }

    private static StyledLine BlankFramed(int width, bool focused)
    {
        var role = focused ? TextRole.FrameFocus : TextRole.Border;

        return new StyledLine(
        [
            new StyledSpan("│", role),
            new StyledSpan(new string(' ', Math.Max(0, width - 2)), TextRole.Body),
            new StyledSpan("│", role),
        ]);
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
    internal static List<StyledSpan> Chips(List<StyledSpan> spans, bool focused)
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
        var cells = ScrollBar.Cells(scroll, BarHeight(document, pane, scroll));

        if (row < 0 || row >= cells.Count || spans[^1] is not { Role: TextRole.Border, Text: [' ', _] } edge)
        {
            return spans;
        }

        spans[^1] = new StyledSpan(GlyphOf(cells[row]), TextRole.Border);
        spans.Add(edge with { Text = edge.Text[1..] });

        return spans;
    }

    /// <summary>
    /// The rows the bar spans: the body rows of the pane, which is one more than the scroll window when a note row
    /// follows the cut text (the Description).
    /// </summary>
    private static int BarHeight(DetailDocument document, int pane, PaneScroll scroll)
    {
        var end = pane + 1 < document.PaneStarts.Count ? document.PaneStarts[pane + 1] : document.Lines.Count;
        var body = end - document.Sections[pane] - 1;

        return Math.Clamp(body, scroll.Viewport, scroll.Viewport + 1);
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

    internal static StyledSpan FocusBorder(StyledSpan span)
    {
        return span with { Role = TextRole.FrameFocus };
    }

    /// <summary>
    /// Shows the cursor row: everything between the borders in the selected role. There is no <c>&gt;</c> mark in any
    /// pane, so every pane marks its cursor the same way (the filled sidebar fields use their own bar).
    /// </summary>
    private static StyledLine CursorLine(StyledLine line)
    {
        return new StyledLine(line.Spans.Select(static span => IsBorderSpan(span) ? span : Selected(span)).ToArray());
    }

    private static StyledSpan Selected(StyledSpan span)
    {
        return span with { Role = TextRole.Selected };
    }

    private static StyledLine RowLine(
        StyledLine drawn,
        int index,
        int cursor,
        LineBlock? block,
        DetailDocument document,
        bool ascii)
    {
        if (block is not null && index >= block.Start && index < block.End)
        {
            var filled = document.SectionIds[document.SectionAt(index)] == PaneIds.Details;

            if (filled)
            {
                return index == block.Start ? drawn : FieldBlock.Focus(drawn, ascii);
            }

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
            || span.Role == TextRole.Editable || span.Text == StatusDot))
        {
            return line;
        }

        return new StyledLine(line.Spans.Select(static span => span.Role switch
        {
            TextRole.Border or TextRole.FrameFocus => span with { Text = SwapBorders(span.Text) },
            TextRole.Editable => span with { Text = span.Text.Replace('▾', 'v') },
            _ when span.Text == StatusDot => span with { Text = "*" },
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
