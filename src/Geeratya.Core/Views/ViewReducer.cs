using Geeratya.Core.Backlog;
using Geeratya.Core.Cache;
using Geeratya.Core.Detail;
using Geeratya.Core.Lists;
using Geeratya.Core.Selection;
using Geeratya.Core.State;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Views;

/// <summary>
/// The tab and view part of the reducer. Pure: ids are drawn from <see cref="AppState.Sequence"/>, time arrives in
/// the commands. A reply is applied only while its tab exists and its request id is the tab's current one; anything
/// else returns the state unchanged (<c>docs/architecture.md</c>, "Views and tabs").
/// </summary>
public static partial class ViewReducer
{
    private const string NoColumnsText = "This view has no columns layout";

    private const string RefreshQueuedText = "Refresh after the write";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    public static ReduceResult Reduce(AppState state, ViewCommand command)
    {
        if (command is Tick tick)
        {
            return Ticked(WhichKeyDelay.Opened(state, tick.Now), tick.Now);
        }

        var effects = new List<Effect>();

        var next = command switch
        {
            ViewOpenRequested open => Open(state, open.Definition, effects),
            TabActivated activated => Activate(state, activated.Tab, effects),
            TabClosed closed => Close(state, closed.Tab, effects),
            ViewLayoutChanged changed => ChangeLayout(state, changed),
            SprintSwitched switched => SwitchSprint(state, switched, effects),
            ViewportChanged viewport => ChangeViewport(state, viewport, effects),
            NextPageRequested requested => NextPage(state, requested.Tab, effects),
            SelectionMoved moved => MoveSelection(state, moved, effects),
            ViewCacheLoaded cached => CacheLoaded(state, cached),
            BoardConfigurationLoaded configured => ConfigurationLoaded(state, configured),
            QuickFiltersLoaded filters => QuickFiltersArrived(state, filters, effects),
            SprintLoaded sprint => SprintArrived(state, sprint),
            SprintsLoaded sprints => SprintsReceived(state, sprints),
            BacklogBecameScrum scrum => BecameScrum(state, scrum),
            ViewTotalLoaded counted => TotalLoaded(state, counted),
            ViewPageLoaded loaded => PageLoaded(state, loaded, effects),
            ViewLoadFailed failed => LoadFailed(state, failed, effects),
            IssueDetailOpenRequested open => DetailReducer.Open(state, open.Tab, open.Key, open.Row, effects),
            IssueDetailBackRequested back => DetailReducer.Back(state, back.Tab),
            IssueDetailCached cached => DetailReducer.IssueCached(state, cached),
            IssueDetailLoaded loaded => DetailReducer.IssueLoaded(state, loaded, effects),
            IssueChildrenLoaded loaded => DetailReducer.ChildrenLoaded(state, loaded),
            IssueCommentsLoaded loaded => DetailReducer.CommentsLoaded(state, loaded),
            IssueRemoteLinksLoaded loaded => DetailReducer.LinksLoaded(state, loaded),
            IssueDetailFailed failed => DetailReducer.LoadFailed(state, failed, effects),
            RefreshRequested refresh => Refresh(state, refresh.Tab, effects),
            LinkChanged changed => ChangeLink(state, changed.Link),
            _ => state,
        };

        return new ReduceResult(next, effects.Count == 0 ? _noEffects : effects);
    }

    private static AppState Open(AppState state, ViewDefinition definition, List<Effect> effects)
    {
        var sequence = state.Sequence;
        var id = new TabId(++sequence);
        var requestId = ++sequence;
        var tab = TabState.Opened(id, definition, requestId, ++sequence, state.ViewportRows);
        var tabs = Stamp(state.Session.Tabs, state.Session.Active, ++sequence);

        effects.Add(new LoadViewPageEffect(id, definition, null, requestId, LoadReason.Open, false));

        return WithSession(state, [.. tabs, tab], id, sequence);
    }

    private static AppState Activate(AppState state, TabId id, List<Effect> effects)
    {
        var session = state.Session;

        if (session.Active == id || Find(session.Tabs, id) is null)
        {
            return state;
        }

        var sequence = state.Sequence;
        var tabs = Stamp(session.Tabs, session.Active, ++sequence);

        return Show(WithSession(state, tabs, session.Active, sequence), id, effects);
    }

    private static AppState Close(AppState state, TabId id, List<Effect> effects)
    {
        var session = state.Session;
        var index = IndexOf(session.Tabs, id);

        if (index < 0)
        {
            return state;
        }

        var remaining = session.Tabs.Where(tab => tab.Id != id).ToArray();

        effects.Add(new CancelTabEffect(id));

        if (session.Active != id)
        {
            return WithSession(state, remaining, session.Active, state.Sequence);
        }

        var closed = WithSession(state, remaining, null, state.Sequence);

        if (remaining.Length == 0)
        {
            return closed;
        }

        var neighbour = remaining[Math.Min(index, remaining.Length - 1)];

        return Show(closed, neighbour.Id, effects);
    }

    private static AppState SwitchSprint(AppState state, SprintSwitched switched, List<Effect> effects)
    {
        var tab = Find(state.Session.Tabs, switched.Tab);
        var value = switched.Sprint.ToString();

        if (tab is null || tab.Definition.Source != ViewSourceKind.Sprint || tab.Definition.SourceValue == value)
        {
            return state;
        }

        var requestId = state.Sequence + 1;
        var definition = tab.Definition with { SourceValue = value, Title = switched.Title };

        // Everything that belonged to the old sprint goes: rows, paging, selection, marks and the cursor. The layout
        // and the board configuration stay.
        var reopened = Emptied(tab with { Definition = definition }, requestId) with
        {
            Sprint = null,
            SprintResolved = false,
        };

        effects.Add(new CancelTabEffect(tab.Id));
        effects.Add(
            new LoadViewPageEffect(
                tab.Id,
                definition,
                null,
                requestId,
                LoadReason.Open,
                false,
                QuickFilterJql.For(tab)));

        return Replace(state, reopened) with { Sequence = requestId };
    }

    /// <summary>
    /// Loads the first page of the tab again from scratch (the quick filters changed): the old load is cancelled and
    /// its late reply ignored, rows, paging, selection and marks go, the layout, board and filters stay.
    /// </summary>
    internal static AppState Reopen(AppState state, TabState tab, List<Effect> effects)
    {
        var requestId = state.Sequence + 1;

        effects.Add(new CancelTabEffect(tab.Id));
        effects.Add(
            new LoadViewPageEffect(
                tab.Id,
                tab.Definition,
                null,
                requestId,
                LoadReason.Open,
                false,
                QuickFilterJql.For(tab)));

        return Replace(state, Emptied(tab, requestId)) with { Sequence = requestId };
    }

    private static TabState Emptied(TabState tab, long requestId)
    {
        return tab with
        {
            Rows = [],
            PendingRows = null,
            Next = null,
            Truncated = false,
            Selected = 0,
            SelectedKey = null,
            Scroll = 0,
            Load = new Loading(LoadReason.Open),
            RequestId = requestId,
            IsStale = false,
            Failures = 0,
            NextRefreshAt = null,
            KeyWidth = 0,
            Cursor = null,
            Total = null,
            TotalIsApproximate = false,
            Marks = [],
            VisualAnchor = null,
            RankPickup = null,
            ProtectedKeys = [],
            ProtectedRows = [],
            RemovedKeys = [],
            RefreshQueued = false,
            Detail = [],
            LoadedAt = null,
        };
    }

    private static AppState ChangeLayout(AppState state, ViewLayoutChanged changed)
    {
        var tab = Find(state.Session.Tabs, changed.Tab);

        if (tab is null || tab.Definition.Layout == changed.Layout)
        {
            return state;
        }

        var layout = tab with { Definition = tab.Definition with { Layout = changed.Layout } };

        // Visual mode exists in the list layouts only; the marks stay.
        return Replace(
            state,
            changed.Layout == ViewLayout.Columns ? layout with { Cursor = null, VisualAnchor = null } : Revealed(layout));
    }

    /// <summary>Scrolls the list so the selected row shows, after the columns layout moved the selection.</summary>
    private static TabState Revealed(TabState tab)
    {
        var position = ListMotion.Reveal(new ListPosition(tab.Selected, tab.Scroll), tab.Rows.Count, tab.VisibleRows);

        return tab with { Scroll = position.Scroll };
    }

    private static AppState ChangeViewport(AppState state, ViewportChanged viewport, List<Effect> effects)
    {
        var height = Math.Max(0, viewport.Rows);
        var columns = viewport.Columns > 0 ? viewport.Columns : state.ViewportColumns;
        var bodyHeight = viewport.BodyHeight >= 0 ? viewport.BodyHeight : state.ViewportBodyHeight;

        if (state.ViewportRows == height
            && state.ViewportColumns == columns
            && state.ViewportBodyHeight == bodyHeight
            && state.Session.Tabs.All(tab => tab.VisibleRows == height))
        {
            return state;
        }

        var tabs = state.Session.Tabs.Select(tab => DetailReducer.Resized(Resized(tab, height), columns)).ToArray();

        var resized = WithSession(state, tabs, state.Session.Active, state.Sequence)
            with { ViewportRows = height, ViewportColumns = columns, ViewportBodyHeight = bodyHeight };

        // A taller viewport can bring the end of the loaded rows within reach without any key press.
        var active = resized.Session.Active is { } id ? Find(resized.Session.Tabs, id) : null;

        return active is not null && WantsNextPage(resized, active) ? RequestNext(resized, active, effects) : resized;
    }

    private static TabState Resized(TabState tab, int height)
    {
        if (BacklogSections.Applies(tab))
        {
            return BacklogCursor.Settle(tab, height);
        }

        var position = ListMotion.Reveal(new ListPosition(tab.Selected, tab.Scroll), tab.Rows.Count, height);

        return tab with { VisibleRows = height, Selected = position.Selected, Scroll = position.Scroll };
    }

    private static TabState GoCold(TabState tab)
    {
        return tab with
        {
            Rows = [],
            Next = null,
            PendingRows = null,
            Truncated = false,
            Total = null,
            TotalIsApproximate = false,
            Scroll = 0,
            Cursor = null,
            KeyWidth = 0,
            RequestId = 0,
            Load = LoadState.Idle,
            Detail = [],
            Cold = true,
        };
    }

    /// <summary>After rows were replaced: selection follows its issue key, else the index is clamped.</summary>
    internal static TabState KeepSelection(TabState tab)
    {
        var rows = tab.Rows;

        if (rows.Count == 0)
        {
            return RowSelection.Pruned(tab with { Selected = 0, SelectedKey = null, Scroll = 0 });
        }

        var found = FindKey(rows, tab.SelectedKey);
        var selected = found >= 0 ? found : Math.Clamp(tab.Selected, 0, rows.Count - 1);

        if (BacklogSections.Applies(tab))
        {
            // The cursor stays on an empty sprint it stands on, however the rows moved.
            if (tab.FocusedSprintId is { } focused)
            {
                var refocused = BacklogCursor.FocusSprint(tab with { Selected = selected }, focused);

                if (refocused.FocusedSprintId is not null)
                {
                    return RowSelection.Pruned(refocused);
                }
            }

            return RowSelection.Pruned(BacklogCursor.Place(tab with { Selected = selected }, selected));
        }

        var position = ListMotion.Reveal(new ListPosition(selected, tab.Scroll), rows.Count, tab.VisibleRows);

        var kept = tab with { Selected = selected, SelectedKey = rows[selected].Key, Scroll = position.Scroll };

        return RowSelection.Pruned(kept);
    }

    private static int FindKey(IReadOnlyList<IssueSummary> rows, string? key)
    {
        if (key is null)
        {
            return -1;
        }

        for (var index = 0; index < rows.Count; index++)
        {
            if (rows[index].Key == key)
            {
                return index;
            }
        }

        return -1;
    }

    private static TabState[] Stamp(IReadOnlyList<TabState> tabs, TabId? active, long stamp)
    {
        return [.. tabs.Select(tab => tab.Id == active ? tab with { LastActiveSeq = stamp } : tab)];
    }

    private static AppState WithSession(AppState state, IReadOnlyList<TabState> tabs, TabId? active, long sequence)
    {
        return state with { Session = state.Session with { Tabs = tabs, Active = active }, Sequence = sequence };
    }

    internal static AppState Replace(AppState state, TabState tab)
    {
        var tabs = state.Session.Tabs.Select(existing => existing.Id == tab.Id ? tab : existing).ToArray();

        return state with { Session = state.Session with { Tabs = tabs } };
    }

    internal static TabState? Find(IReadOnlyList<TabState> tabs, TabId id)
    {
        var index = IndexOf(tabs, id);

        return index < 0 ? null : tabs[index];
    }

    private static int IndexOf(IReadOnlyList<TabState> tabs, TabId id)
    {
        for (var index = 0; index < tabs.Count; index++)
        {
            if (tabs[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }
}
