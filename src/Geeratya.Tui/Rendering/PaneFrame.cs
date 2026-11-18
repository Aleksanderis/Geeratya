using Geeratya.Core.Panes;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// Draws a one-column body (list, backlog, empty states) as the screen frame: a top edge with the title chips, the
/// content lines between the side borders, shared edges where <see cref="PaneDivider"/>s say, and a bottom edge. Where
/// the body is too small for a frame the content is returned as it is.
/// </summary>
public static class PaneFrame
{
    // Frame rows above the first row of a list: the top edge and the column titles.
    private const int TitleRows = 1;

    public static List<StyledLine> Build(
        int width,
        int height,
        IReadOnlyList<StyledSpan>? left,
        IReadOnlyList<StyledSpan>? right,
        IReadOnlyList<StyledLine> content,
        IReadOnlyList<PaneDivider> dividers,
        int focusContentIndex = -1,
        PaneScroll? scroll = null)
    {
        if (!FrameGrid.CanFrame(width, height))
        {
            return [.. content];
        }

        var edges = dividers
            .Where(divider => divider.ContentIndex >= 0 && divider.ContentIndex < height - 2)
            .OrderBy(divider => divider.ContentIndex)
            .DistinctBy(divider => divider.ContentIndex)
            .ToList();
        var panes = Panes(width, height, edges);
        var focused = FocusedPane(panes, focusContentIndex + 1);
        var grid = new FrameGrid(width, height, panes, focused);
        var edgeRows = edges.ToDictionary(divider => divider.ContentIndex + 1);
        var lines = new List<StyledLine>(height);

        for (var row = 0; row < height; row++)
        {
            if (row == 0)
            {
                lines.Add(Edge(grid, row, FrameGrid.Chips(panes[0], left, right, focused == 0)));
            }
            else if (edgeRows.TryGetValue(row, out var divider))
            {
                var paneIndex = panes.FindIndex(candidate => candidate.Row == row);

                lines.Add(Edge(
                    grid,
                    row,
                    FrameGrid.Chips(panes[paneIndex], divider.Left, divider.Right, focused == paneIndex)));
            }
            else if (row == height - 1)
            {
                lines.Add(Edge(grid, row, []));
            }
            else
            {
                var line = row - 1 < content.Count ? content[row - 1] : StyledLine.Empty;

                lines.Add(new StyledLine(grid.Row(row, (_, length) => StyledLines.Fit(line, length).Spans)));
            }
        }

        return scroll is { Scrollable: true } ? Marked(lines, scroll, edgeRows.Keys) : lines;
    }

    /// <summary>
    /// The frame with the scroll markers of a cut list in its right border column (the frame has no padding column):
    /// <c>▲</c> on the first and <c>▼</c> on the last row of the list that is no shared edge, when rows are hidden above
    /// or below, the same marker an overlay box shows in its right padding column
    /// (<see cref="OverlayBox.ScrollMarker"/>). <paramref name="scroll"/> counts the rows of the list below its title
    /// line.
    /// </summary>
    private static List<StyledLine> Marked(List<StyledLine> lines, PaneScroll scroll, IEnumerable<int> edgeRows)
    {
        var rows = Enumerable.Range(TitleRows + 1, Math.Max(0, lines.Count - TitleRows - 2))
            .Where(row => !edgeRows.Contains(row))
            .ToList();

        if (rows.Count == 0)
        {
            return lines;
        }

        var first = scroll.ClampedOffset;

        if (first > 0)
        {
            lines[rows[0]] = WithMarker(lines[rows[0]], OverlayBox.ScrollMarker(0, scroll.Viewport, first, scroll.Total));
        }

        if (first + scroll.Viewport < scroll.Total)
        {
            var last = rows[^1];

            lines[last] = WithMarker(lines[last], OverlayBox.ScrollMarker(scroll.Viewport - 1, scroll.Viewport, first, scroll.Total));
        }

        return lines;
    }

    private static StyledLine WithMarker(StyledLine line, string marker)
    {
        if (line.Spans.Count == 0 || line.Spans[^1] is not { Role: TextRole.Border or TextRole.FrameFocus, Text.Length: 1 })
        {
            return line;
        }

        var spans = line.Spans.ToList();

        spans[^1] = new StyledSpan(marker, TextRole.Muted);

        return new StyledLine(spans);
    }

    /// <summary>The pane that holds the frame row (the last one starting at or above it); the first when none is given.</summary>
    private static int FocusedPane(List<PaneRect> panes, int frameRow)
    {
        return Math.Max(0, panes.FindLastIndex(pane => pane.Row <= frameRow));
    }

    /// <summary>
    /// The top or the bottom edge of a frame around lines the caller has already put between side borders (the issue
    /// detail document draws its own sides and shared edges).
    /// </summary>
    public static StyledLine OuterEdge(
        int width,
        int height,
        bool top,
        IReadOnlyList<StyledSpan>? left,
        IReadOnlyList<StyledSpan>? right,
        bool focused = true)
    {
        var pane = new PaneRect(0, 0, height, width);
        var grid = new FrameGrid(width, height, [pane], focused ? 0 : -1);

        return top ? Edge(grid, 0, FrameGrid.Chips(pane, left, right, focused)) : Edge(grid, height - 1, []);
    }

    private static StyledLine Edge(FrameGrid grid, int row, IReadOnlyList<EdgeChip> chips)
    {
        return new StyledLine(grid.Row(row, static (_, _) => [], chips));
    }

    private static List<PaneRect> Panes(int width, int height, List<PaneDivider> edges)
    {
        var rows = new List<int> { 0 };

        rows.AddRange(edges.Select(divider => divider.ContentIndex + 1));
        rows.Add(height - 1);

        var panes = new List<PaneRect>(rows.Count - 1);

        for (var index = 0; index < rows.Count - 1; index++)
        {
            panes.Add(new PaneRect(rows[index], 0, rows[index + 1] - rows[index] + 1, width));
        }

        return panes;
    }
}
