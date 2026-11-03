using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// Draws a one-column body (list, backlog, empty states) as the screen frame: a top edge with the title chips, the
/// content lines between the side borders, shared edges where <see cref="PaneDivider"/>s say, and a bottom edge. Where
/// the body is too small for a frame the content is returned as it is.
/// </summary>
public static class PaneFrame
{
    public static List<StyledLine> Build(
        int width,
        int height,
        IReadOnlyList<StyledSpan>? left,
        IReadOnlyList<StyledSpan>? right,
        IReadOnlyList<StyledLine> content,
        IReadOnlyList<PaneDivider> dividers)
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
        var grid = new FrameGrid(width, height, panes);
        var edgeRows = edges.ToDictionary(divider => divider.ContentIndex + 1);
        var lines = new List<StyledLine>(height);

        for (var row = 0; row < height; row++)
        {
            if (row == 0)
            {
                lines.Add(Edge(grid, row, FrameGrid.Chips(panes[0], left, right)));
            }
            else if (edgeRows.TryGetValue(row, out var divider))
            {
                var pane = panes.First(candidate => candidate.Row == row);

                lines.Add(Edge(grid, row, FrameGrid.Chips(pane, divider.Left, divider.Right)));
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

        return lines;
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
        IReadOnlyList<StyledSpan>? right)
    {
        var pane = new PaneRect(0, 0, height, width);
        var grid = new FrameGrid(width, height, [pane]);

        return top ? Edge(grid, 0, FrameGrid.Chips(pane, left, right)) : Edge(grid, height - 1, []);
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
