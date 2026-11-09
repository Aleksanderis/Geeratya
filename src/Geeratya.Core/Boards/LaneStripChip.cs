namespace Geeratya.Core.Boards;

/// <summary>One lane in the lane strip above the columns (#874).</summary>
/// <param name="Key">The lane key (<see cref="SwimlaneLane.Key"/>).</param>
/// <param name="Mark">The fold mark: <c>▾</c> for the lane whose columns are shown, <c>▸</c> for the others.</param>
/// <param name="Label">The lane name and its count.</param>
/// <param name="Start">The first body cell of the chip.</param>
/// <param name="End">The cell after the last one.</param>
/// <param name="Current">Whether this lane's columns are the ones shown.</param>
public sealed record LaneStripChip(string Key, string Mark, string Label, int Start, int End, bool Current)
{
    /// <summary>The chip text.</summary>
    public string Text => $"{Mark} {Label}";
}
