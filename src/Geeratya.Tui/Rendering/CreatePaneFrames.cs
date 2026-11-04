using Geeratya.Core.Detail;
using Geeratya.Core.Mouse;
using Geeratya.Core.Panes;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// Draws the panes of the create form with the frame of the issue detail (<see cref="Panel"/>): the focused pane gets
/// the accent frame and the filled chip, a pane with more rows than fit scrolls to keep its focus row in view and shows
/// the scrollbar in its right padding column. Collects the mouse rows of what it draws. Pure.
/// </summary>
internal sealed class CreatePaneFrames
{
    private readonly bool _ascii;
    private readonly List<OverlayRegion> _regions;

    public CreatePaneFrames(bool ascii, List<OverlayRegion> regions)
    {
        _ascii = ascii;
        _regions = regions;
    }

    /// <summary>
    /// The pane as <paramref name="inner"/> + 2 lines of <paramref name="width"/> columns; <paramref name="top"/> and
    /// <paramref name="left"/> are where it sits in the overlay, for the mouse rows.
    /// </summary>
    public List<StyledLine> Build(CreatePaneSpec spec, int width, int inner, int top, int left)
    {
        var rows = Math.Max(0, inner);
        var pinned = spec.Pinned is null || rows < 2 ? 0 : 1;
        var room = rows - pinned;
        var first = FirstVisible(spec, room);
        var scroll = new PaneScroll(first, spec.Rows.Count, room);
        var cells = ScrollBar.Cells(scroll, room);
        var content = new List<StyledLine>(rows);
        var contentWidth = Panel.InnerWidth(width);

        for (var row = 0; row < room; row++)
        {
            var index = first + row;
            var shown = index < spec.Rows.Count ? spec.Rows[index] : null;

            content.Add(StyledLines.Fit(shown?.Line ?? StyledLine.Empty, contentWidth));

            if (shown is { Target: { } target })
            {
                _regions.Add(new OverlayRegion(target, top + 1 + row, left + 1, width - 2, 1, shown.Index));
            }
        }

        if (pinned > 0)
        {
            content.Add(StyledLines.Fit(spec.Pinned!, contentWidth));
            _regions.Add(new OverlayRegion(MouseTarget.FormMoreFields, top + 1 + room, left + 1, width - 2, 1, 0));
        }

        var right = spec.Right;

        if (scroll.Scrollable && right is not null)
        {
            right = new List<StyledSpan> { new(scroll.Chip(), TextRole.Muted) };
        }

        var lines = new List<StyledLine>(rows + Panel.EdgeRows);

        Panel.Add(lines, spec.Title, content, width, right);

        return [.. lines.Select((line, index) => Styled(line, index, spec.Focused, cells))];
    }

    private StyledLine Styled(StyledLine line, int index, bool focused, IReadOnlyList<ScrollCell> cells)
    {
        var spans = line.Spans.ToList();

        if (index == 0)
        {
            spans = IssueDetailRenderer.Chips(spans, focused);
        }
        else if (index - 1 < cells.Count && spans.Count > 1)
        {
            spans = Scrollbar(spans, cells[index - 1]);
        }

        if (focused)
        {
            spans = [.. spans.Select(FocusBorder)];
        }

        return new StyledLine(spans);
    }

    private static StyledSpan FocusBorder(StyledSpan span)
    {
        return span.Role == TextRole.Border ? IssueDetailRenderer.FocusBorder(span) : span;
    }

    // The right padding cell of a content row becomes the scrollbar cell of its row.
    private List<StyledSpan> Scrollbar(List<StyledSpan> spans, ScrollCell cell)
    {
        if (spans[^1] is not { Role: TextRole.Border, Text: [' ', _] } edge)
        {
            return spans;
        }

        spans[^1] = new StyledSpan(GlyphOf(cell), TextRole.Border);
        spans.Add(edge with { Text = edge.Text[1..] });

        return spans;
    }

    private string GlyphOf(ScrollCell cell)
    {
        return cell switch
        {
            ScrollCell.Up => "▲",
            ScrollCell.Thumb => _ascii ? "#" : "█",
            ScrollCell.Track => _ascii ? "." : "░",
            _ => "▼",
        };
    }

    private static int FirstVisible(CreatePaneSpec spec, int room)
    {
        var end = spec.FocusStart + Math.Max(1, spec.FocusLength);
        var most = Math.Max(0, spec.Rows.Count - room);

        if (room <= 0 || end <= room)
        {
            return 0;
        }

        return Math.Clamp(Math.Min(spec.FocusStart, end - room), 0, most);
    }
}
