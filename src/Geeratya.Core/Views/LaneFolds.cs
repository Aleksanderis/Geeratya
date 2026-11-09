using Geeratya.Core.Boards;
using Geeratya.Core.State;

namespace Geeratya.Core.Views;

/// <summary>
/// Folding a swimlane to its header row while the lanes are stacked (#893): <c>lane.fold</c> (<c>z c</c>) folds the
/// current lane and moves to the next open one, <c>lane.unfold</c> (<c>z o</c>) opens the next folded lane and moves
/// to it, and a click on the chevron of a lane header toggles that lane. Entering a folded lane by any other way
/// (<c>]]</c>, a click, a flash jump) opens it, so the current lane is never folded. Folds are per tab and last for the
/// session. Pure on a <see cref="TabState"/>.
/// </summary>
public static class LaneFolds
{
    /// <summary>The refusal when the lanes are not stacked: a lane's columns are the only thing shown.</summary>
    public const string NotStackedText = "Folding a lane needs stacked lanes (<space>vl).";

    /// <summary>The refusal when the current lane is the only open one.</summary>
    public const string LastOpenText = "Keep one lane open: unfold another lane first (z o).";

    /// <summary>The notice when no drawn lane is folded.</summary>
    public const string NoneFoldedText = "No lane is folded.";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary><c>lane.fold</c>: folds the current lane and moves to the next open lane below it (above it at the end).</summary>
    public static ReduceResult Fold(AppState state)
    {
        if (Active(state) is not { } tab || !LaneRegions.IsStacked(tab))
        {
            return Refused(state, NotStackedText);
        }

        var current = ColumnsNavigation.CurrentLaneKey(tab);
        var target = Neighbour(tab, current, static region => !region.Folded);

        if (target is null)
        {
            return Refused(state, LastOpenText);
        }

        var name = NameOf(tab, current);
        var folded = ColumnsNavigation.SwitchLane(tab with { FoldedLanes = [.. tab.FoldedLanes, current] }, target);

        return new ReduceResult(ViewReducer.Replace(state, folded) with { Message = $"Folded lane {name}." }, _noEffects);
    }

    /// <summary><c>lane.unfold</c>: opens the next folded lane below the current one (wrapping) and moves to it.</summary>
    public static ReduceResult Unfold(AppState state)
    {
        if (Active(state) is not { } tab || !LaneRegions.IsStacked(tab))
        {
            return Refused(state, NotStackedText);
        }

        var target = Neighbour(tab, ColumnsNavigation.CurrentLaneKey(tab), static region => region.Folded);

        if (target is null)
        {
            return Refused(state, NoneFoldedText);
        }

        var opened = ColumnsNavigation.SwitchLane(tab, target);

        return new ReduceResult(ViewReducer.Replace(state, opened) with { Message = $"Opened lane {NameOf(tab, target)}." }, _noEffects);
    }

    /// <summary>
    /// A click on the chevron of lane <paramref name="key"/>: opens it when folded (and moves to it), folds it when open
    /// (the current lane hands the cursor to the next open one). The tab is returned as it is when the fold would leave
    /// no lane open.
    /// </summary>
    public static TabState ToggleAt(TabState tab, string key)
    {
        if (LaneRegions.IsFolded(tab, key))
        {
            return ColumnsNavigation.SwitchLane(tab, key);
        }

        var current = ColumnsNavigation.CurrentLaneKey(tab);

        if (key == current)
        {
            return Neighbour(tab, current, static region => !region.Folded) is { } target
                ? ColumnsNavigation.SwitchLane(tab with { FoldedLanes = [.. tab.FoldedLanes, key] }, target)
                : tab;
        }

        return ColumnsNavigation.Settle(tab with { FoldedLanes = [.. tab.FoldedLanes, key] });
    }

    private static string? Neighbour(TabState tab, string current, Func<LaneRegion, bool> wanted)
    {
        var regions = LaneRegions.Regions(tab);
        var index = Math.Max(0, regions.ToList().FindIndex(region => region.Key == current));

        for (var step = 1; step < regions.Count; step++)
        {
            var region = regions[(index + step) % regions.Count];

            if (wanted(region))
            {
                return region.Key;
            }
        }

        return null;
    }

    private static string NameOf(TabState tab, string key)
    {
        return ColumnsNavigation.LanesOf(tab).First(lane => lane.Key == key).Name;
    }

    private static TabState? Active(AppState state)
    {
        var tab = state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;

        return tab is { Definition.Layout: ViewLayout.Columns, Detail.Count: 0 } ? tab : null;
    }

    private static ReduceResult Refused(AppState state, string text)
    {
        return new ReduceResult(state with { Message = text }, _noEffects);
    }
}
