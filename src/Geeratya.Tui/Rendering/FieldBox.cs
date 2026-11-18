using Geeratya.Core.Detail;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The one frame of a field box (<c>docs/internal/ui-consistency.md</c>, component 3): the rounded border of the issue
/// detail's panes with the label as a chip in the top edge, a focused box drawn in the focus border with its chip
/// filled. The Summary and Description boxes of the create form, the boxes of the field forms (sprint, link, edit)
/// and the panes of the create form draw their frame here, so the same box looks the same wherever it appears. Pure.
/// </summary>
internal static class FieldBox
{
    /// <summary>
    /// The box: its top edge with <paramref name="title"/> (and the dim <paramref name="right"/> chip), the
    /// <paramref name="rows"/> between the walls, the bottom edge. Rows are fitted to the inner width, text that does
    /// not fit ends in the cut glyph.
    /// </summary>
    public static List<StyledLine> Build(
        IReadOnlyList<StyledSpan> title,
        IReadOnlyList<StyledLine> rows,
        int width,
        bool focused,
        IReadOnlyList<StyledSpan>? right = null)
    {
        var inner = Panel.InnerWidth(width);
        var lines = new List<StyledLine>(rows.Count + Panel.EdgeRows);

        Panel.Add(lines, title, [.. rows.Select(row => StyledLines.FitMarked(row, inner))], width, right);

        return Restyle(lines, focused);
    }

    /// <summary>
    /// The lines of a box for its focus state: the title chip of the top edge filled (or dim) and, when focused, every
    /// border cell in the focus role.
    /// </summary>
    public static List<StyledLine> Restyle(IReadOnlyList<StyledLine> lines, bool focused)
    {
        return [.. lines.Select((line, index) => Restyle(line, index == 0, focused))];
    }

    private static StyledLine Restyle(StyledLine line, bool top, bool focused)
    {
        var spans = top ? IssueDetailRenderer.Chips([.. line.Spans], focused) : [.. line.Spans];

        if (focused)
        {
            spans = [.. spans.Select(static span => span.Role == TextRole.Border ? IssueDetailRenderer.FocusBorder(span) : span)];
        }

        return new StyledLine(spans);
    }
}
