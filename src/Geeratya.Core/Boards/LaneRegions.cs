using Geeratya.Core.Lists;
using Geeratya.Core.Views;

namespace Geeratya.Core.Boards;

/// <summary>
/// Where each lane's columns sit when several lanes are open at once (#891). The body under the lane strip is split
/// equally between as many lanes as fit at <see cref="MinLaneRows"/> each; the lanes drawn are the page of that many
/// lanes holding the current one (pages of the same size from the first lane, the last page ending at the last
/// lane), so stepping inside a page never moves the others. Without stacking, or when fewer than two lanes fit, the
/// one current lane takes the whole body, as before. The reducer's scroll maths, the hit tests, the flash labels and
/// the renderer all read these regions. Pure; O(lanes) and no allocation for the current lane's region unless a lane is
/// folded (#893), which adds a pass over the few lanes drawn.
/// </summary>
public static class LaneRegions
{
    /// <summary>The rows of a lane header above its columns.</summary>
    public const int HeaderRows = 1;

    /// <summary>The cells at the start of a lane header that hold its chevron: a click there folds or opens the lane.</summary>
    public const int ChevronCells = 2;

    /// <summary>The fewest rows a stacked lane takes: its header and a board of seven rows (edges and five lines).</summary>
    public const int MinLaneRows = 8;

    /// <summary>Whether the tab draws several lanes' columns: stacking on, a grouping with at least two lanes that fit.</summary>
    public static bool IsStacked(TabState tab)
    {
        return Shown(tab, ColumnsLanes(tab).Count) >= 2;
    }

    /// <summary>The region of the current lane (the whole body under the strip when not stacked).</summary>
    public static LaneRegion FocusRegion(TabState tab)
    {
        var lanes = ColumnsLanes(tab);
        var strip = Strip(tab);
        var shown = Shown(tab, lanes.Count);
        var current = Swimlanes.Current(lanes, tab.Lane);

        if (shown < 2 || current is null)
        {
            return new LaneRegion(current?.Key ?? string.Empty, strip, Body(tab) - strip, false);
        }

        var index = IndexOf(lanes, current.Key);

        return RegionAt(tab, strip, shown, PageFirst(lanes.Count, shown, index), index);
    }

    /// <summary>The regions drawn, top to bottom; one for the current lane when not stacked.</summary>
    public static IReadOnlyList<LaneRegion> Regions(TabState tab)
    {
        var lanes = ColumnsLanes(tab);
        var shown = Shown(tab, lanes.Count);

        if (shown < 2)
        {
            return [FocusRegion(tab)];
        }

        var strip = Strip(tab);
        var first = PageFirst(lanes.Count, shown, IndexOf(lanes, Swimlanes.Current(lanes, tab.Lane)!.Key));
        var regions = new List<LaneRegion>(shown);

        for (var position = 0; position < shown; position++)
        {
            regions.Add(RegionAt(tab, strip, shown, first, first + position));
        }

        return regions;
    }

    /// <summary>The region holding a body row, null above or below them (the strip row, rows past the body).</summary>
    public static LaneRegion? RegionAtRow(TabState tab, int row)
    {
        foreach (var region in Regions(tab))
        {
            if (region.Contains(row))
            {
                return region;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether lane <paramref name="key"/> is folded to its header row: stacked lanes only, and never the current lane
    /// (entering a lane opens it, and a lane that became current by other means is drawn open).
    /// </summary>
    public static bool IsFolded(TabState tab, string key)
    {
        return tab.FoldedLanes.Count > 0
            && tab.FoldedLanes.Contains(key)
            && key != Swimlanes.Current(ColumnsLanes(tab), tab.Lane)?.Key
            && IsStacked(tab);
    }

    private static LaneRegion RegionAt(TabState tab, int strip, int shown, int first, int laneIndex)
    {
        var lanes = ColumnsLanes(tab);
        var folded = 0;

        for (var position = 0; position < shown; position++)
        {
            if (IsFolded(tab, lanes[first + position].Key))
            {
                folded++;
            }
        }

        // Folded lanes keep their header row; the rest of the body is shared between the open lanes.
        var open = shown - folded;
        var available = Body(tab) - strip - folded;
        var share = available / open;
        var extra = available % open;
        var top = strip;
        var openBefore = 0;

        for (var position = 0; position < laneIndex - first; position++)
        {
            if (IsFolded(tab, lanes[first + position].Key))
            {
                top += HeaderRows;

                continue;
            }

            top += share + (openBefore < extra ? 1 : 0);
            openBefore++;
        }

        var key = lanes[laneIndex].Key;

        if (IsFolded(tab, key))
        {
            return new LaneRegion(key, top, HeaderRows, true, true);
        }

        return new LaneRegion(key, top, share + (openBefore < extra ? 1 : 0), true);
    }

    /// <summary>
    /// Whether the lane strip is left out: every lane is drawn with its own header, so the strip would only repeat
    /// them and its row goes to the lanes. The strip stays while some lane is not drawn, as the list of all of them.
    /// </summary>
    public static bool StripHidden(TabState tab)
    {
        var count = ColumnsLanes(tab).Count;

        var shown = Shown(tab, count);

        return shown >= 2 && shown == count;
    }

    private static int Strip(TabState tab)
    {
        if (!LaneStrip.IsShown(tab))
        {
            return 0;
        }

        return StripHidden(tab) ? 0 : LaneStrip.Rows;
    }

    private static int Shown(TabState tab, int laneCount)
    {
        if (!tab.StackLanes || laneCount < 2 || !LaneStrip.IsShown(tab))
        {
            return 0;
        }

        // Counted with the strip's row taken: when all lanes fit that way the strip goes and they still fit.
        var available = Body(tab) - LaneStrip.Rows;

        return Math.Min(laneCount, available / MinLaneRows);
    }

    private static int PageFirst(int laneCount, int shown, int index)
    {
        return Math.Min(index / shown * shown, laneCount - shown);
    }

    private static int IndexOf(IReadOnlyList<SwimlaneLane> lanes, string key)
    {
        for (var index = 0; index < lanes.Count; index++)
        {
            if (lanes[index].Key == key)
            {
                return index;
            }
        }

        return 0;
    }

    private static int Body(TabState tab)
    {
        return Math.Max(0, tab.VisibleRows + ListLayout.HeaderRows + ListLayout.FooterRows);
    }

    private static IReadOnlyList<SwimlaneLane> ColumnsLanes(TabState tab)
    {
        return tab.Columns.GetLanes(tab);
    }
}
