using Geeratya.Core.Text;
using Geeratya.Core.Views;

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
    private const int MinimumWidth = 8;

    /// <summary>Whether the tab draws the strip: the columns layout with a grouping and rows to group.</summary>
    public static bool IsShown(TabState tab)
    {
        return tab.Swimlane != SwimlaneKind.None
            && tab.Definition.Layout == ViewLayout.Columns
            && tab.Rows.Count > 0;
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
    /// The chips for <paramref name="lanes"/> in <paramref name="width"/> cells, scrolled so the current lane shows
    /// whole (cut with <c>…</c> only when it alone is wider than the row). Linear in the lanes.
    /// </summary>
    public static LaneStripLayout Layout(IReadOnlyList<SwimlaneLane> lanes, string? laneKey, int width)
    {
        if (Swimlanes.Current(lanes, laneKey) is not { } current || width < MinimumWidth)
        {
            return new LaneStripLayout([], false, false);
        }

        var index = lanes.ToList().IndexOf(current);
        var used = DisplayWidth.Of(Text(current, true));
        var first = index;

        // Take earlier lanes while they still fit next to the current one (room for the more-mark on the left).
        while (first > 0)
        {
            var more = first - 1 > 0 ? MoreMarkWidth : 0;
            var next = used + Gap + DisplayWidth.Of(Text(lanes[first - 1], false));

            if (next + more > width - 1)
            {
                break;
            }

            used = next;
            first--;
        }

        return Place(lanes, current, first, width);
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
