using Geeratya.Core.Boards;
using Geeratya.Core.State;
using Geeratya.Jira.Boards.Models;

namespace Geeratya.Core.Views;

/// <summary>
/// The view options of a board tab (#192): quick filters (<c>view.quick-filter</c>, <c>:quickfilter</c>), which
/// narrow the board source with JQL and load it again, and swimlanes (<c>view.swimlane</c>, <c>]]</c> and
/// <c>[[</c>), which only choose what the columns layout shows of the rows already loaded. Both belong to the tab.
/// </summary>
public static class BoardViewOptions
{
    public const string NoBoardText = "Quick filters and swimlanes belong to a board view.";
    public const string NoFiltersText = "This board has no quick filters.";
    public const string FiltersLoadingText = "Loading the quick filters...";
    public const string FiltersLoadingCommandText = "Loading the quick filters: run the command again in a moment.";
    public const string NoColumnsText = "Swimlanes need the columns layout of a board (<space>l).";
    public const string FilterUsage = "Usage: :quickfilter [off|<name>|<number>]";
    public const string NoLanesText = "No lanes: the board has no issues.";

    private const string LaneKeysText = "]] and [[ change the lane";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary><c>view.quick-filter</c>: switches to the next filter alone; after the last, all are off.</summary>
    public static ReduceResult CycleFilter(AppState state)
    {
        if (Check(state) is not { } tab)
        {
            return Refused(state, NoBoardText);
        }

        if (!tab.QuickFiltersKnown)
        {
            return LoadFilters(state, tab, true);
        }

        if (tab.QuickFilters.Count == 0)
        {
            return Refused(state, NoFiltersText);
        }

        var next = NextAlone(tab);

        return Apply(state, tab, next is null ? [] : [next.Id]);
    }

    /// <summary>
    /// <c>:quickfilter</c>: without an argument lists the filters, <c>off</c> clears them, a name or a 1-based number
    /// switches that filter on or off (several can be on; their clauses are joined with AND).
    /// </summary>
    public static ReduceResult FilterCommand(AppState state, string argument)
    {
        if (Check(state) is not { } tab)
        {
            return Refused(state, NoBoardText);
        }

        if (!tab.QuickFiltersKnown)
        {
            return LoadFilters(state, tab, false);
        }

        if (tab.QuickFilters.Count == 0)
        {
            return Refused(state, NoFiltersText);
        }

        if (argument.Length == 0)
        {
            return Refused(state, Describe(tab));
        }

        if (argument is "off" or "none" or "clear")
        {
            return Apply(state, tab, []);
        }

        if (Find(tab, argument) is not { } found)
        {
            return Refused(state, $"No quick filter \"{argument}\". {FilterUsage}");
        }

        var active = tab.ActiveQuickFilters.Contains(found.Id)
            ? tab.ActiveQuickFilters.Where(id => id != found.Id).ToArray()
            : [.. tab.ActiveQuickFilters, found.Id];

        return Apply(state, tab, active);
    }

    /// <summary>The names of the switched-on filters, for the header; empty when none is on.</summary>
    public static IReadOnlyList<string> ActiveNames(TabState tab)
    {
        return
        [
            .. tab.QuickFilters
                .Where(filter => tab.ActiveQuickFilters.Contains(filter.Id))
                .Select(filter => filter.Name),
        ];
    }

    /// <summary>
    /// What the header says about the options: <c>Filter: A + B</c> and <c>Lane 2/5: Name</c>, separated by
    /// <paramref name="separator"/>; empty when none is in use.
    /// </summary>
    public static string Summary(TabState tab, string separator)
    {
        var parts = new List<string>();
        var names = ActiveNames(tab);

        if (names.Count > 0)
        {
            parts.Add($"Filter: {string.Join(" + ", names)}");
        }

        if (tab.Swimlane != SwimlaneKind.None && tab.Definition.Layout == ViewLayout.Columns)
        {
            var lanes = Swimlanes.Lanes(tab.Swimlane, tab.Rows);

            if (Swimlanes.Current(lanes, tab.Lane) is { } lane)
            {
                parts.Add($"Lane {lanes.ToList().IndexOf(lane) + 1}/{lanes.Count}: {lane.Name}");
            }
        }

        return string.Join(separator, parts);
    }

    /// <summary><c>view.swimlane</c>: the next grouping (none, assignee, epic); the first lane is shown.</summary>
    public static ReduceResult CycleSwimlane(AppState state)
    {
        if (Check(state) is not { } tab)
        {
            return Refused(state, NoBoardText);
        }

        if (tab.Definition.Layout != ViewLayout.Columns)
        {
            return Refused(state, NoColumnsText);
        }

        var kind = Swimlanes.Next(tab.Swimlane);
        var grouped = ColumnsNavigation.Resync(tab with { Swimlane = kind, Lane = null });
        var lanes = Swimlanes.Lanes(kind, grouped.Rows);
        var noun = lanes.Count == 1 ? "lane" : "lanes";
        var text = kind == SwimlaneKind.None
            ? "Swimlanes off."
            : $"Swimlanes by {Swimlanes.Label(kind)}: {lanes.Count} {noun} ({LaneKeysText}).";

        return new ReduceResult(ViewReducer.Replace(state, grouped) with { Message = text }, _noEffects);
    }

    /// <summary>
    /// <c>]]</c> and <c>[[</c> in the columns layout: the next or previous lane, wrapping around. Null when the tab
    /// shows no lanes, so the caller goes on with what the key does elsewhere.
    /// </summary>
    public static ReduceResult? StepLane(AppState state, int direction)
    {
        var tab = state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;

        if (tab is not { Swimlane: not SwimlaneKind.None, Definition.Layout: ViewLayout.Columns, Detail.Count: 0 })
        {
            return null;
        }

        var lanes = Swimlanes.Lanes(tab.Swimlane, tab.Rows);

        if (Swimlanes.Step(lanes, tab.Lane, direction) is not { } lane)
        {
            return Refused(state, NoLanesText);
        }

        var moved = ColumnsNavigation.Resync(tab with { Lane = lane.Key });

        return new ReduceResult(ViewReducer.Replace(state, moved) with { Message = null }, _noEffects);
    }

    private static TabState? Check(AppState state)
    {
        var tab = state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;

        return tab is not null && tab.Definition.Source.IsBoardBased() ? tab : null;
    }

    private static ReduceResult LoadFilters(AppState state, TabState tab, bool apply)
    {
        var effect = new LoadQuickFiltersEffect(tab.Id, tab.Definition, apply);
        var text = apply ? FiltersLoadingText : FiltersLoadingCommandText;

        return new ReduceResult(state with { Message = text }, [effect]);
    }

    private static ReduceResult Refused(AppState state, string text)
    {
        return new ReduceResult(state with { Message = text }, _noEffects);
    }

    private static QuickFilter? NextAlone(TabState tab)
    {
        if (tab.ActiveQuickFilters.Count == 0)
        {
            return tab.QuickFilters[0];
        }

        if (tab.ActiveQuickFilters is not [var only])
        {
            return null;
        }

        var index = tab.QuickFilters.ToList().FindIndex(filter => filter.Id == only);

        return index >= 0 && index + 1 < tab.QuickFilters.Count ? tab.QuickFilters[index + 1] : null;
    }

    private static QuickFilter? Find(TabState tab, string argument)
    {
        if (int.TryParse(argument, out var number))
        {
            return number >= 1 && number <= tab.QuickFilters.Count ? tab.QuickFilters[number - 1] : null;
        }

        return tab.QuickFilters.FirstOrDefault(
            filter => string.Equals(filter.Name, argument, StringComparison.OrdinalIgnoreCase));
    }

    private static string Describe(TabState tab)
    {
        var parts = tab.QuickFilters.Select((filter, index) => Entry(tab, filter, index));

        return $"Quick filters: {string.Join(", ", parts)}. {FilterUsage}";
    }

    private static string Entry(TabState tab, QuickFilter filter, int index)
    {
        var on = tab.ActiveQuickFilters.Contains(filter.Id) ? " (on)" : string.Empty;

        return $"{index + 1} {filter.Name}{on}";
    }

    private static ReduceResult Apply(AppState state, TabState tab, IReadOnlyList<long> active)
    {
        var changed = tab with { ActiveQuickFilters = active, Lane = null };
        var effects = new List<Effect>();
        var reloaded = ViewReducer.Reopen(state, changed, effects);
        var names = ActiveNames(changed);
        var text = names.Count == 0 ? "Quick filters off." : $"Quick filter: {string.Join(" + ", names)}.";

        return new ReduceResult(reloaded with { Message = text }, effects);
    }
}
