using Geeratya.Core.Text;

namespace Geeratya.Core.Detail;

/// <summary>The lines of one issue detail laid out for one width; the screen shows a window of them.</summary>
/// <param name="Width">The columns the lines were laid out for.</param>
/// <param name="Lines">The styled lines, each at most <paramref name="Width"/> columns wide.</param>
/// <param name="Targets">One entry per line: what Enter on that line opens, null for a plain line.</param>
/// <param name="Sections">The line index of the first content line of each section (panel), ascending.</param>
/// <param name="Stops">
/// The lines the cursor can rest on, ascending: every line except the panel edges (title and bottom border).
/// </param>
public sealed record DetailDocument(
    int Width,
    IReadOnlyList<StyledLine> Lines,
    IReadOnlyList<DetailTarget?> Targets,
    IReadOnlyList<int> Sections,
    IReadOnlyList<int> Stops)
{
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
        return Stops.Count == 0 ? 0 : Stops.FirstOrDefault(stop => stop >= line, Stops[^1]);
    }
}
