using Geeratya.Core.Detail;
using Geeratya.Core.Startup;
using Geeratya.Core.State;

namespace Geeratya.Core.Views;

public static partial class ViewReducer
{
    private static AppState NextPage(AppState state, TabId id, List<Effect> effects)
    {
        var tab = Find(state.Session.Tabs, id);

        return tab is null ? state : RequestNext(state, tab, effects);
    }

    /// <summary>
    /// Issues the next page when the tab can take one: a cursor exists, nothing is in flight (one page per tab) and
    /// the rows are not cold or capped.
    /// </summary>
    private static AppState RequestNext(AppState state, TabState tab, List<Effect> effects)
    {
        if (tab.Cold || tab.Truncated || tab.Next is null || tab.Load is Loading)
        {
            return state;
        }

        var requestId = state.Sequence + 1;
        var loading = tab with { Load = new Loading(LoadReason.NextPage), RequestId = requestId };

        effects.Add(
            new LoadViewPageEffect(
                tab.Id,
                tab.Definition,
                tab.Next,
                requestId,
                LoadReason.NextPage,
                false,
                QuickFilterJql.For(tab)));

        return Replace(state, loading) with { Sequence = requestId };
    }

    /// <summary>
    /// The automatic trigger: rows exist, the tab is idle (a failed tab waits for an explicit retry, so a held key
    /// never hammers a failing Jira) and the selection is within one screen of the last loaded row (the last row
    /// itself counts when the height is unknown).
    /// </summary>
    private static bool WantsNextPage(AppState state, TabState tab)
    {
        if (tab.Rows.Count == 0 || tab.Load is not IdleState)
        {
            return false;
        }

        // Board and backlog sources need every card for the columns: they walk all pages, scrolled or not.
        if (LoadsAllPages(tab.Definition.Source))
        {
            return true;
        }

        if (state.Session.Active != tab.Id)
        {
            return false;
        }

        return tab.Selected >= tab.Rows.Count - Math.Max(1, tab.VisibleRows);
    }

    private static bool LoadsAllPages(ViewSourceKind source)
    {
        return source.IsBoardBased();
    }

    /// <summary>
    /// The board configuration of a board or backlog load: kept in the tab, applied only while the request is the
    /// tab's current one.
    /// </summary>
    private static AppState ConfigurationLoaded(AppState state, BoardConfigurationLoaded configured)
    {
        var tab = Find(state.Session.Tabs, configured.Tab);

        if (tab is null || tab.RequestId != configured.RequestId || tab.Load is not Loading)
        {
            return state;
        }

        return Replace(
            state,
            DetailReducer.WithEstimateLabel(tab with { Board = configured.Configuration }, state.ViewportColumns));
    }

    /// <summary>
    /// The quick filters of the board: kept in the tab (the lookup has no request id; the runner drops the replies
    /// of an old site). A filter that is switched on but no longer offered is switched off (the next load goes
    /// without it).
    /// </summary>
    private static AppState QuickFiltersArrived(AppState state, QuickFiltersLoaded loaded, List<Effect> effects)
    {
        var tab = Find(state.Session.Tabs, loaded.Tab);

        if (tab is null)
        {
            return state;
        }

        if (tab.AwaitingFilters)
        {
            return FiltersForFirstPage(state, tab, loaded, effects);
        }

        if (loaded.Failure is not null)
        {
            return state with { Message = loaded.Failure, MessageSeverity = StatusSeverity.Warning };
        }

        var active = tab.ActiveQuickFilters.Where(id => loaded.Filters.Any(filter => filter.Id == id)).ToArray();
        var stored = Replace(
            state,
            tab with { QuickFilters = loaded.Filters, QuickFiltersKnown = true, ActiveQuickFilters = active });

        if (!loaded.Apply || state.Session.Active != tab.Id)
        {
            return stored;
        }

        var applied = BoardViewOptions.CycleFilter(stored);
        effects.AddRange(applied.Effects);

        return applied.State;
    }

    /// <summary>
    /// The filters a restored tab waited for arrived (or failed): its first page now loads with their clauses, or
    /// without any when they are gone.
    /// </summary>
    private static AppState FiltersForFirstPage(AppState state, TabState tab, QuickFiltersLoaded loaded, List<Effect> effects)
    {
        var known = loaded.Failure is null;

        // A failed lookup keeps the remembered ids (the file must not lose them over one offline start).
        var active = known
            ? tab.ActiveQuickFilters.Where(id => loaded.Filters.Any(filter => filter.Id == id)).ToArray()
            : tab.ActiveQuickFilters;

        if (tab.Load is not Loading { Reason: LoadReason.Open })
        {
            return Replace(state, tab with { AwaitingFilters = false });
        }

        var ready = tab with
        {
            AwaitingFilters = false,
            QuickFilters = known ? loaded.Filters : [],
            QuickFiltersKnown = known,
            ActiveQuickFilters = active,
        };

        effects.Add(
            new LoadViewPageEffect(
                tab.Id,
                tab.Definition,
                null,
                tab.RequestId,
                LoadReason.Open,
                false,
                QuickFilterJql.For(ready)));

        var stored = Replace(state, ready);

        return known ? stored : stored with { Message = loaded.Failure, MessageSeverity = StatusSeverity.Warning };
    }

    /// <summary>
    /// Reopens the remembered tabs without loading any of them but the active one (the others load when shown, from
    /// the cache first). The tabs are marked <see cref="TabState.Restored"/>.
    /// </summary>
    internal static AppState Restore(AppState state, IReadOnlyList<RestoredTab> restored, int active, List<Effect> effects)
    {
        var sequence = state.Sequence;
        var tabs = new List<TabState>();

        foreach (var entry in restored)
        {
            var id = new TabId(++sequence);
            var opened = TabState.Opened(id, entry.Definition, 0, ++sequence, state.ViewportRows);

            tabs.Add(
                opened with
                {
                    Load = LoadState.Idle,
                    Cold = true,
                    Restored = true,
                    ActiveQuickFilters = entry.QuickFilters,
                    Swimlane = entry.Swimlane,
                    Lane = entry.Lane,
                    Name = entry.Name,
                });
        }

        var shown = WithSession(state, tabs, null, sequence);

        return Show(shown, tabs[Math.Clamp(active, 0, tabs.Count - 1)].Id, effects);
    }

    /// <summary>The sprint of a Sprint view: kept in the tab while the request is the tab's current one.</summary>
    private static AppState SprintArrived(AppState state, SprintLoaded loaded)
    {
        var tab = Find(state.Session.Tabs, loaded.Tab);

        if (tab is null || tab.RequestId != loaded.RequestId || tab.Load is not Loading)
        {
            return state;
        }

        return Replace(state, tab with { Sprint = loaded.Sprint, SprintResolved = !loaded.LookupFailed });
    }

    /// <summary>
    /// The open and future sprints of a Scrum backlog or Sprint view load (#504): kept in the tab for the section headers, applied only while
    /// the request is the tab's current one.
    /// </summary>
    private static AppState SprintsReceived(AppState state, SprintsLoaded loaded)
    {
        var tab = Find(state.Session.Tabs, loaded.Tab);

        if (tab is null || tab.RequestId != loaded.RequestId || tab.Load is not Loading)
        {
            return state;
        }

        var withSprints = tab with { Sprints = loaded.Sprints };

        return Replace(state, tab.Rows.Count == 0 ? withSprints : KeepSelection(withSprints));
    }

    /// <summary>A plain backlog proved to be a Scrum board: the tab takes the Scrum backlog source while its load runs.</summary>
    private static AppState BecameScrum(AppState state, BacklogBecameScrum scrum)
    {
        var tab = Find(state.Session.Tabs, scrum.Tab);

        if (tab is null
            || tab.RequestId != scrum.RequestId
            || tab.Load is not Loading
            || tab.Definition.Source != ViewSourceKind.Backlog)
        {
            return state;
        }

        return Replace(state, tab with { Definition = tab.Definition with { Source = ViewSourceKind.ScrumBacklog } });
    }

    /// <summary>A Kanban board load applies the Done cutoff: the tab remembers it for the last column's title.</summary>
    private static AppState CutoffApplied(AppState state, DoneCutoffApplied applied)
    {
        var tab = Find(state.Session.Tabs, applied.Tab);

        if (tab is null || tab.RequestId != applied.RequestId || tab.Load is not Loading)
        {
            return state;
        }

        return Replace(state, tab with { DoneCutoffApplied = true });
    }

    /// <summary>The approximate count of a JQL source; ignored when the tab is gone or is not a JQL source.</summary>
    private static AppState TotalLoaded(AppState state, ViewTotalLoaded counted)
    {
        var tab = Find(state.Session.Tabs, counted.Tab);

        if (tab is null || LoadsAllPages(tab.Definition.Source))
        {
            return state;
        }

        return Replace(state, tab with { Total = counted.Total, TotalIsApproximate = true });
    }
}
