using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Boards;

/// <summary>
/// The lane strip of the columns layout (#874): one row above the frame that lists every lane of the active grouping
/// (<c>▾ Name n</c> for the lane whose columns are shown, <c>▸ Name n</c> for the others), so all lanes are visible
/// together and a click on one shows it. It costs a fixed row while a grouping is on; the reducer's scroll maths, the
/// hit tests and the renderer all take their board height from <see cref="BoardRows"/>. Pure.
/// </summary>
public static class LaneStrip
{
    /// <summary>The rows the strip takes above the frame.</summary>
    public const int Rows = 1;

    private const string CurrentMark = "▾";
    private const string OtherMark = "▸";
    private const int MaxNameWidth = 24;
    private const int Gap = 2;
    private const int MoreMarkWidth = 2;

    /// <summary>Whether the tab draws the strip: the columns layout with a grouping.</summary>
    public static bool IsShown(TabState tab)
    {
        return tab.Swimlane != SwimlaneKind.None && tab.Definition.Layout == ViewLayout.Columns;
    }

    /// <summary>The list rows left for the board: the tab's visible rows less the strip.</summary>
    public static int BoardRows(TabState tab)
    {
        return IsShown(tab) ? Math.Max(0, tab.VisibleRows - Rows) : tab.VisibleRows;
    }

    /// <summary>The rows above the frame (the strip), for converting body rows to board rows.</summary>
    public static int Offset(TabState tab)
    {
        return IsShown(tab) ? Rows : 0;
    }

    /// <summary>
    /// The chips for <paramref name="rows"/> in <paramref name="width"/> cells, scrolled so the current lane shows
    /// whole (cut with <c>…</c> only when it alone is wider than the row).
    /// </summary>
    public static LaneStripLayout Layout(
        SwimlaneKind kind,
        string? laneKey,
        IReadOnlyList<IssueSummary> rows,
        int width)
    {
        var lanes = Swimlanes.Lanes(kind, rows);

        if (Swimlanes.Current(lanes, laneKey) is not { } current || width <= 0)
        {
            return new LaneStripLayout([], false, false);
        }

        var index = lanes.ToList().IndexOf(current);
        var first = 0;

        while (first < index && !Fits(lanes, first, index, width))
        {
            first++;
        }

        return Place(lanes, current, first, width);
    }

    private static bool Fits(IReadOnlyList<SwimlaneLane> lanes, int first, int index, int width)
    {
        var end = first > 0 ? MoreMarkWidth : 0;

        for (var lane = first; lane <= index; lane++)
        {
            end += DisplayWidth.Of(Text(lanes[lane], lane == index));
            end += lane == index ? 0 : Gap;
        }

        return end <= width - 1;
    }

    private static LaneStripLayout Place(IReadOnlyList<SwimlaneLane> lanes, SwimlaneLane current, int first, int width)
    {
        var chips = new List<LaneStripChip>();
        var start = first > 0 ? MoreMarkWidth : 0;
        var cut = false;

        for (var index = first; index < lanes.Count; index++)
        {
            var lane = lanes[index];
            var isCurrent = lane.Key == current.Key;
            var text = Text(lane, isCurrent);
            var room = (index == lanes.Count - 1 ? width : width - 1) - start;

            if (DisplayWidth.Of(text) > room && !isCurrent)
            {
                cut = true;

                break;
            }

            var shown = DisplayWidth.Fit(text, Math.Max(1, room));
            var end = start + DisplayWidth.Of(shown);

            var label = shown.Length > 2 ? shown[2..] : string.Empty;

            chips.Add(new LaneStripChip(lane.Key, shown[..1], label, start, end, isCurrent));
            start = end + Gap;
        }

        return new LaneStripLayout(chips, first > 0, cut);
    }

    private static string Text(SwimlaneLane lane, bool current)
    {
        var name = DisplayWidth.Fit(lane.Name, MaxNameWidth);

        return $"{(current ? CurrentMark : OtherMark)} {name} {lane.Count}";
    }
}
