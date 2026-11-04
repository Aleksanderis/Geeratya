using Geeratya.Core.Text;

namespace Geeratya.Core.Detail;

/// <summary>The lines of one issue detail laid out for one width; the screen shows a window of them.</summary>
/// <param name="Width">The columns the lines were laid out for.</param>
/// <param name="Lines">The styled lines, each at most <paramref name="Width"/> columns wide.</param>
/// <param name="Targets">One entry per line: what Enter on that line opens, null for a plain line.</param>
/// <param name="Sections">The line index of the first content line of each section (panel), ascending.</param>
/// <param name="Stops">
/// The lines the cursor can rest on, ascending: every line except the shared panel edges.
/// </param>
public sealed record DetailDocument(
    int Width,
    IReadOnlyList<StyledLine> Lines,
    IReadOnlyList<DetailTarget?> Targets,
    IReadOnlyList<int> Sections,
    IReadOnlyList<int> Stops)
{
    /// <summary>The title of each section, parallel to <see cref="Sections"/>: what the top edge of the frame shows.</summary>
    public IReadOnlyList<IReadOnlyList<StyledSpan>> PaneTitles { get; init; } = [];

    /// <summary>The fold id of each section, parallel to <see cref="Sections"/>; the first (the fields) is never folded.</summary>
    public IReadOnlyList<string> SectionIds { get; init; } = [];

    /// <summary>The right-hand chip text of each section, parallel to <see cref="PaneTitles"/>; empty for none (only the header carries one: its status).</summary>
    public IReadOnlyList<IReadOnlyList<StyledSpan>> PaneRightTitles { get; init; } = [];

    /// <summary>The line ranges that are one cursor stop each (a wrapped field), ascending; other lines are their own stop.</summary>
    public IReadOnlyList<LineBlock> Blocks { get; init; } = [];

    /// <summary>The block that holds <paramref name="line"/>, null when the line stands alone.</summary>
    public LineBlock? BlockAt(int line)
    {
        return Blocks.FirstOrDefault(block => line >= block.Start && line < block.End);
    }

    /// <summary>The index of the section that holds <paramref name="line"/>; a shared edge line still belongs to the pane above it.</summary>
    public int SectionAt(int line)
    {
        var index = 0;

        for (var section = 0; section < Sections.Count; section++)
        {
            var edge = section == 0 ? 0 : Sections[section] - 1;

            if (edge < line)
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
