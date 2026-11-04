using Geeratya.Core.Panes;
using Geeratya.Core.Text;

namespace Geeratya.Core.Detail;

/// <summary>The lines of one issue detail laid out for one width; the screen shows a window of them.</summary>
/// <param name="Width">The columns the lines were laid out for.</param>
/// <param name="Lines">The styled lines, each at most <paramref name="Width"/> columns wide.</param>
/// <param name="Targets">One entry per line: what Enter on that line opens, null for a plain line.</param>
/// <param name="Sections">The line index of the first content line of each pane, ascending.</param>
/// <param name="Stops">
/// The lines the cursor can rest on, ascending: every line except the pane edges.
/// </param>
public sealed record DetailDocument(
    int Width,
    IReadOnlyList<StyledLine> Lines,
    IReadOnlyList<DetailTarget?> Targets,
    IReadOnlyList<int> Sections,
    IReadOnlyList<int> Stops)
{
    /// <summary>
    /// The title of each section, parallel to <see cref="Sections"/>: what the top edge of the frame shows.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<StyledSpan>> PaneTitles { get; init; } = [];

    /// <summary>The line of the top edge of each pane, parallel to <see cref="Sections"/>.</summary>
    public IReadOnlyList<int> PaneStarts { get; init; } = [];

    /// <summary>
    /// The inner scroll of each pane that shows a scrollbar, parallel to <see cref="Sections"/>; null for a pane that
    /// fits.
    /// </summary>
    public IReadOnlyList<PaneScroll?> PaneScrolls { get; init; } = [];

    /// <summary>The pane id of each section (<see cref="PaneIds"/>), parallel to <see cref="Sections"/>.</summary>
    public IReadOnlyList<string> SectionIds { get; init; } = [];

    /// <summary>
    /// The right-hand chip text of each section, parallel to <see cref="PaneTitles"/>; empty for none (only the header carries one: its status).
    /// </summary>
    public IReadOnlyList<IReadOnlyList<StyledSpan>> PaneRightTitles { get; init; } = [];

    /// <summary>
    /// The line ranges that are one cursor stop each (a wrapped field), ascending; other lines are their own stop.
    /// </summary>
    public IReadOnlyList<LineBlock> Blocks { get; init; } = [];

    /// <summary>
    /// The comment id of every content row of the Comments pane (all rows, not only the shown window), null for rows
    /// that belong to no comment; empty without a Comments pane.
    /// </summary>
    public IReadOnlyList<string?> CommentRowIds { get; init; } = [];

    /// <summary>The rows the screen showed when the document was laid out; null when it was laid out in full.</summary>
    public int? ViewportRows { get; init; }

    /// <summary>The block that holds <paramref name="line"/>, null when the line stands alone.</summary>
    public LineBlock? BlockAt(int line)
    {
        return Blocks.FirstOrDefault(block => line >= block.Start && line < block.End);
    }

    /// <summary>The index of the pane that holds <paramref name="line"/>, edges included.</summary>
    public int SectionAt(int line)
    {
        var index = 0;

        for (var section = 0; section < PaneStarts.Count; section++)
        {
            if (PaneStarts[section] <= line)
            {
                index = section;
            }
        }

        return index;
    }

    /// <summary>The first stop after <paramref name="line"/>, or the last stop when there is none.</summary>
    public int StopAfter(int line)
    {
        return Stops.Count == 0 ? 0 : Stops.FirstOrDefault(stop => stop > line, Stops[^1]);
    }

    /// <summary>The last stop before <paramref name="line"/>, or the first stop when there is none.</summary>
    public int StopBefore(int line)
    {
        return Stops.Count == 0 ? 0 : Stops.LastOrDefault(stop => stop < line, Stops[0]);
    }

    /// <summary>The stop at <paramref name="line"/>, else the first one below it, else the last one.</summary>
    public int Snap(int line)
    {
        var from = BlockAt(line)?.Start ?? line;

        return Stops.Count == 0 ? 0 : Stops.FirstOrDefault(stop => stop >= from, Stops[^1]);
    }
}
