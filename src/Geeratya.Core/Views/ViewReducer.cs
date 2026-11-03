using Geeratya.Core.Backlog;
using Geeratya.Core.Boards;
using Geeratya.Core.Cache;
using Geeratya.Core.Detail;
using Geeratya.Core.Lists;
using Geeratya.Core.Selection;
using Geeratya.Core.Startup;
using Geeratya.Core.State;
using Geeratya.Core.Writes;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Views;

/// <summary>
/// The tab and view part of the reducer. Pure: ids are drawn from <see cref="AppState.Sequence"/>, time arrives in
/// the commands. A reply is applied only while its tab exists and its request id is the tab's current one; anything
/// else returns the state unchanged (<c>docs/architecture.md</c>, "Views and tabs").
/// </summary>
public static class ViewReducer
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

    private static AppState MoveSelection(AppState state, SelectionMoved moved, List<Effect> effects)
    {
        var tab = Find(state.Session.Tabs, moved.Tab);

        if (tab is null || tab.Rows.Count == 0)
        {
            return state;
        }

        var index = Math.Clamp(moved.Index, 0, tab.Rows.Count - 1);

        if (BacklogSections.Applies(tab))
        {
            var placed = BacklogCursor.Place(tab, index);
            var shown = Replace(state, placed);

            return WantsNextPage(shown, placed) ? RequestNext(shown, placed, effects) : shown;
        }

        var position = ListMotion.Reveal(new ListPosition(index, tab.Scroll), tab.Rows.Count, tab.VisibleRows);
        var selected = tab with { Selected = index, SelectedKey = tab.Rows[index].Key, Scroll = position.Scroll };
        var next = Replace(state, selected);

        return WantsNextPage(next, selected) ? RequestNext(next, selected, effects) : next;
    }

    /// <summary>
    /// Moves the selection of the active tab's list by a keyboard motion and goes through <see cref="SelectionMoved"/>,
    /// so scrolling and the next-page trigger have one path. Other layouts and tabs without rows are left alone.
    /// </summary>
    public static ReduceResult Navigate(AppState state, ListMove move)
    {
        var tab = state.Session.Active is { } id ? Find(state.Session.Tabs, id) : null;

        if (tab is { Detail.Count: > 0 })
        {
            return new ReduceResult(DetailReducer.MoveCursor(state, tab, move), _noEffects);
        }

        if (tab is null || (tab.Rows.Count == 0 && !BacklogSections.Applies(tab)))
        {
            return new ReduceResult(state, _noEffects);
        }

        if (tab.Definition.Layout == ViewLayout.Columns)
        {
            return MovedInColumns(state, ColumnsNavigation.Move(tab, move));
        }

        if (BacklogSections.Applies(tab))
        {
            return MovedInColumns(state, BacklogCursor.Move(tab, move));
        }

        var position = ListMotion.Move(
            new ListPosition(tab.Selected, tab.Scroll),
            move,
            1,
            tab.Rows.Count,
            tab.VisibleRows);

        return Reduce(state, new SelectionMoved(tab.Id, position.Selected));
    }

    /// <summary>
    /// A click on the body of the active tab: selects the row or card under the pointer, or puts the cursor on the
    /// line of the opened issue. Null when the pointer is on nothing selectable (a blank row, a title, the area
    /// under the cards), so the caller knows no action may follow.
    /// </summary>
    public static ReduceResult? PlaceCursor(AppState state, PointerPlaced placed)
    {
        var tab = state.Session.Active is { } id ? Find(state.Session.Tabs, id) : null;

        if (tab is null)
        {
            return null;
        }

        if (tab.Detail.Count > 0)
        {
            var placedLine = DetailReducer.PlaceCursor(state, tab, placed.Row);

            return placedLine is null ? null : new ReduceResult(placedLine, _noEffects);
        }

        if (tab.Rows.Count == 0 && !BacklogSections.Applies(tab))
        {
            return null;
        }

        if (tab.Definition.Layout == ViewLayout.Columns)
        {
            var moved = ColumnsNavigation.Place(tab, placed.Width, placed.Column, placed.Row);

            return ReferenceEquals(moved, tab) ? null : MovedInColumns(state, moved);
        }

        if (BacklogSections.Applies(tab))
        {
            var line = tab.Scroll + placed.Row - ListLayout.HeaderRows;

            if (placed.Row < ListLayout.HeaderRows || line >= tab.Scroll + tab.VisibleRows)
            {
                return null;
            }

            var layout = BacklogSections.Build(tab);

            if (BacklogCursor.EmptySprintAtLine(layout, line) is { } sprintId)
            {
                return new ReduceResult(Replace(state, BacklogCursor.FocusSprint(tab, sprintId)), _noEffects);
            }

            return BacklogCursor.RowAtLine(layout, line) is { } found
                ? Reduce(state, new SelectionMoved(tab.Id, found))
                : null;
        }

        var index = tab.Scroll + placed.Row - ListLayout.HeaderRows;

        if (placed.Row < ListLayout.HeaderRows || index >= tab.Rows.Count || index >= tab.Scroll + tab.VisibleRows)
        {
            return null;
        }

        return Reduce(state, new SelectionMoved(tab.Id, index));
    }

    /// <summary>Enter: opens the selected row as an issue, or loads more comments inside an issue.</summary>
    public static ReduceResult OpenSelected(AppState state)
    {
        var effects = new List<Effect>();
        var next = DetailReducer.OpenSelected(state, effects);

        return new ReduceResult(next, effects.Count == 0 ? _noEffects : effects);
    }

    /// <summary>Whether Esc has an issue to pop on the active tab.</summary>
    public static bool CanGoBack(AppState state)
    {
        return DetailReducer.CanGoBack(state);
    }

    /// <summary>
    /// Moves the selection of the active tab between the columns of its board (<c>h</c>, <c>l</c>, <c>0</c>,
    /// <c>$</c>). The list layout has no columns and ignores it.
    /// </summary>
    public static ReduceResult NavigateColumn(AppState state, BoardColumnMove move)
    {
        var tab = state.Session.Active is { } id ? Find(state.Session.Tabs, id) : null;

        if (tab is null || tab.Definition.Layout != ViewLayout.Columns || tab.Rows.Count == 0)
        {
            return new ReduceResult(state, _noEffects);
        }

        return MovedInColumns(state, ColumnsNavigation.MoveColumn(tab, move));
    }

    /// <summary>
    /// Switches the active tab between its layouts (<c>view.layout-toggle</c>). Only a board has a columns layout;
    /// any other source says so.
    /// </summary>
    public static ReduceResult ToggleLayout(AppState state)
    {
        var tab = state.Session.Active is { } id ? Find(state.Session.Tabs, id) : null;

        if (tab is null)
        {
            return new ReduceResult(state, _noEffects);
        }

        var layout = tab.Definition.Layout == ViewLayout.Columns ? ViewLayout.List : ViewLayout.Columns;

        return SetLayout(state, layout);
    }

    /// <summary>Switches the active tab to <paramref name="layout"/> (<c>:layout list|columns</c>).</summary>
    public static ReduceResult SetLayout(AppState state, ViewLayout layout)
    {
        var tab = state.Session.Active is { } id ? Find(state.Session.Tabs, id) : null;

        if (tab is null)
        {
            return new ReduceResult(state, _noEffects);
        }

        if (layout == ViewLayout.Columns && tab.Definition.Source is not (ViewSourceKind.Board or ViewSourceKind.Sprint))
        {
            return new ReduceResult(state with { Message = NoColumnsText }, _noEffects);
        }

        return Reduce(state, new ViewLayoutChanged(tab.Id, layout));
    }

    private static ReduceResult MovedInColumns(AppState state, TabState moved)
    {
        var effects = new List<Effect>();
        var next = Replace(state, moved);

        if (WantsNextPage(next, moved))
        {
            next = RequestNext(next, moved, effects);
        }

        return new ReduceResult(next, effects.Count == 0 ? _noEffects : effects);
    }

    /// <summary>
    /// Makes the tab <paramref name="delta"/> places from the active one active, wrapping around the ends. Nothing
    /// happens with fewer than two tabs.
    /// </summary>
    public static ReduceResult StepTab(AppState state, int delta)
    {
        var tabs = state.Session.Tabs;
        var current = state.Session.Active is { } id ? IndexOf(tabs, id) : -1;

        if (tabs.Count < 2 || current < 0)
        {
            return new ReduceResult(state, _noEffects);
        }

        var target = (((current + delta) % tabs.Count) + tabs.Count) % tabs.Count;

        return Reduce(state, new TabActivated(tabs[target].Id));
    }

    /// <summary>
    /// Flips to the tab that was active before the current one (the other tab with the newest
    /// <see cref="TabState.LastActiveSeq"/>); a closed tab is gone, so it is never picked. With no other tab the
    /// status line says so.
    /// </summary>
    public static ReduceResult LastActiveTab(AppState state)
    {
        var previous = state.Session.Tabs
            .Where(tab => tab.Id != state.Session.Active)
            .MaxBy(tab => tab.LastActiveSeq);

        if (previous is null || state.Session.Active is null)
        {
            return new ReduceResult(state with { Message = "No previous tab." }, _noEffects);
        }

        return Reduce(state, new TabActivated(previous.Id));
    }

    /// <summary>Makes the tab with the 1-based <paramref name="number"/> active; a missing number says so.</summary>
    public static ReduceResult GoToTab(AppState state, int number)
    {
        var tabs = state.Session.Tabs;

        if (number < 1 || number > tabs.Count)
        {
            return new ReduceResult(state with { Message = $"No tab {number}." }, _noEffects);
        }

        return Reduce(state, new TabActivated(tabs[number - 1].Id));
    }

    /// <summary>Names the active tab; an empty name restores the source label. Local only, nothing goes to Jira.</summary>
    public static ReduceResult RenameActiveTab(AppState state, string name)
    {
        if (state.Session.Active is not { } id || Find(state.Session.Tabs, id) is not { } tab)
        {
            return new ReduceResult(state with { Message = "No tab to rename.", MessageSeverity = StatusSeverity.Warning }, _noEffects);
        }

        var clean = TabNames.Clean(name);
        var renamed = Replace(state, tab with { Name = clean });
        var message = clean is null ? "Tab name cleared: showing the source label." : $"Tab renamed to \"{clean}\".";

        return new ReduceResult(renamed with { Message = message }, _noEffects);
    }

    /// <summary>Closes the active tab (cancelling its work); without one the status line says so.</summary>
    public static ReduceResult CloseActiveTab(AppState state)
    {
        if (state.Session.Active is not { } id)
        {
            return new ReduceResult(state with { Message = "No tab to close." }, _noEffects);
        }

        return Reduce(state, new TabClosed(id));
    }

    /// <summary>
    /// The clock tick: refreshes the active tab in the background when its schedule says so. Returns the same state
    /// instance and no effects when nothing is due, which is almost every tick.
    /// </summary>
    private static ReduceResult Ticked(AppState state, DateTimeOffset now)
    {
        var tab = state.Session.Active is { } id ? Find(state.Session.Tabs, id) : null;

        // Writes first: a refresh under a running write would replace rows the write is about to settle. The due
        // time stays in the past, so the first tick after the last operation ends refreshes.
        if (state.Session.Operations.Count > 0 || tab is null || !IsDue(state.Session.Link, tab, now))
        {
            return new ReduceResult(state, _noEffects);
        }

        var effects = new List<Effect>(1);

        return new ReduceResult(StartRefresh(state, tab, true, effects), effects);
    }

    /// <summary>
    /// An automatic refresh is due when the tab is idle (or backing off after a failed load), its schedule has
    /// passed, the link allows traffic and the rows are a first page that can be replaced as a whole.
    /// </summary>
    private static bool IsDue(SiteLink link, TabState tab, DateTimeOffset now)
    {
        if (tab.Cold || tab.Load is Loading || tab.Load is Failed { Reason: LoadReason.NextPage })
        {
            return false;
        }

        if (tab.NextRefreshAt is not { } due || now < due)
        {
            return false;
        }

        var paused = link is Rejected || (link is RateLimited limited && limited.Until > now);

        return !paused && !HasMoreThanFirstPage(tab);
    }

    /// <summary>
    /// A list tab that loaded further pages is not refreshed on its own: tokens only go forward, so replacing the
    /// rows would throw the reader's position away every minute. The manual key resets it.
    /// </summary>
    private static bool HasMoreThanFirstPage(TabState tab)
    {
        return tab.Definition.Source is ViewSourceKind.Jql or ViewSourceKind.SavedFilter
            && tab.Rows.Count > ViewPaging.PageSize;
    }

    internal static AppState StartRefresh(AppState state, TabState tab, bool background, List<Effect> effects)
    {
        var requestId = state.Sequence + 1;

        // A board tab whose configuration never arrived (the open failed on it) fetches it with this refresh, which
        // only a user-lane refresh does, so the clock's refresh is promoted to that lane.
        var needsConfiguration = tab.Board is null && LoadsAllPages(tab.Definition.Source);
        background = background && !needsConfiguration;

        var loading = tab with
        {
            Load = new Loading(LoadReason.Refresh, background),
            RequestId = requestId,
            PendingRows = null,
        };

        effects.Add(
            new LoadViewPageEffect(
                tab.Id,
                tab.Definition,
                null,
                requestId,
                LoadReason.Refresh,
                background,
                QuickFilterJql.For(tab)));

        return Replace(state, loading) with { Sequence = requestId };
    }

    /// <summary>
    /// The refresh key: user lane, never a second load while one is in flight. A failed next page is retried as a
    /// next page (the rows stay and the reader keeps the position).
    /// </summary>
    private static AppState Refresh(AppState state, TabId id, List<Effect> effects)
    {
        var tab = Find(state.Session.Tabs, id);

        if (tab is null || tab.Cold || tab.Load is Loading)
        {
            return state;
        }

        if (tab.Load is Failed { Reason: LoadReason.NextPage } && tab.Rows.Count > 0 && tab.Next is not null)
        {
            return RequestNext(state, tab, effects);
        }

        if (state.Session.Operations.Count > 0)
        {
            // Not dropped: it runs when the last write ends (WriteReducer).
            return Replace(state, tab with { RefreshQueued = true }) with { Message = RefreshQueuedText };
        }

        return StartRefresh(state, tab, false, effects);
    }

    /// <summary>
    /// Refreshes the active tab (the <c>view.refresh</c> key); without one the status line says so.
    /// </summary>
    public static ReduceResult RefreshActive(AppState state)
    {
        if (state.Session.Active is not { } id)
        {
            return new ReduceResult(state with { Message = "No view to refresh." }, _noEffects);
        }

        return Reduce(state, new RefreshRequested(id));
    }

    /// <summary>A rate-limit state from the transport; "online" clears only a rate-limit state.</summary>
    private static AppState ChangeLink(AppState state, SiteLink link)
    {
        if (link is Online && state.Session.Link is not RateLimited)
        {
            return state;
        }

        // A rate-limit wait does not hide a rejected login: it would be forgotten when the wait ends.
        if (link is RateLimited && state.Session.Link is Rejected)
        {
            return state;
        }

        return WithLink(state, link);
    }

    private static AppState WithLink(AppState state, SiteLink link)
    {
        return state with { Session = state.Session with { Link = link } };
    }

    /// <summary>What a failed reply says about the link: a 401 rejects it, an unreachable site is offline.</summary>
    private static SiteLink LinkAfterFailure(SiteLink link, JiraErrorKind kind, DateTimeOffset at)
    {
        return kind switch
        {
            JiraErrorKind.Unauthorized => new Rejected(),
            JiraErrorKind.Network or JiraErrorKind.Timeout or JiraErrorKind.HostRefused =>
                link is Offline ? link : new Offline(at),
            _ => link,
        };
    }

    /// <summary>
    /// A continued load came back with a rejected page token: start again at the first page, keeping the rows on
    /// screen and the selection by issue key, instead of showing an error.
    /// </summary>
    private static AppState Restart(AppState state, TabState tab, List<Effect> effects)
    {
        var requestId = state.Sequence + 1;
        var loading = tab with { Load = new Loading(LoadReason.Retry), RequestId = requestId, Next = null };

        effects.Add(
            new LoadViewPageEffect(
                tab.Id,
                tab.Definition,
                null,
                requestId,
                LoadReason.Retry,
                false,
                QuickFilterJql.For(tab)));

        return Replace(state, loading) with { Sequence = requestId };
    }

    private static IReadOnlyList<IssueSummary> CombinedRows(
        TabState tab,
        ViewPageLoaded loaded,
        bool appended,
        bool gathering)
    {
        if (appended)
        {
            return Append(tab.Rows, loaded.Page.Items);
        }

        return gathering && tab.PendingRows is { } gathered ? Append(gathered, loaded.Page.Items) : loaded.Page.Items;
    }

    /// <summary>
    /// A board refresh page that is not the last: keeps the rows gathered so far aside and asks for the next page
    /// with the same lane. The rows on screen stay until the last page arrives.
    /// </summary>
    private static AppState GatherMore(
        AppState state,
        TabState tab,
        Loading loading,
        IReadOnlyList<IssueSummary> gathered,
        PageCursor cursor,
        List<Effect> effects)
    {
        var requestId = state.Sequence + 1;
        var gathering = tab with { PendingRows = gathered, RequestId = requestId };

        effects.Add(
            new LoadViewPageEffect(
                tab.Id,
                tab.Definition,
                cursor,
                requestId,
                LoadReason.Refresh,
                loading.Background,
                QuickFilterJql.For(tab)));

        return EnforceTotal(Replace(state, gathering) with { Sequence = requestId }, effects);
    }

    private static IReadOnlyList<IssueSummary> MergedRows(
        AppState state,
        TabState tab,
        ViewPageLoaded loaded,
        IReadOnlyList<IssueSummary> combined,
        bool appended)
    {
        if (appended)
        {
            return Append(tab.Rows, WriteMerge.KeepHeldRows(state, tab, loaded.Page.Items));
        }

        return WriteMerge.KeepHeldRows(state, tab, combined);
    }

    private static AppState PageLoaded(AppState state, ViewPageLoaded loaded, List<Effect> effects)
    {
        var tab = Find(state.Session.Tabs, loaded.Tab);

        if (tab is null || tab.RequestId != loaded.RequestId || tab.Load is not Loading loading)
        {
            return state;
        }

        var appended = loading.Reason == LoadReason.NextPage;
        var gathering = loading.Reason == LoadReason.Refresh && LoadsAllPages(tab.Definition.Source);
        var combined = CombinedRows(tab, loaded, appended, gathering);

        // A board refresh swaps its rows in with the last page, so the screen never shows a half-refreshed board.
        if (gathering && loaded.Page.Next is { } more && combined.Count < ViewPaging.MaxRowsPerTab)
        {
            return GatherMore(state, tab, loading, combined, more, effects);
        }

        // A reply that lands while a write runs (or started before one ended) keeps the tab's own row for its keys.
        var merged = MergedRows(state, tab, loaded, combined, appended);
        var truncated = IsOverCap(combined.Count, loaded.Page.Next);
        var capped = truncated ? merged.Take(ViewPaging.MaxRowsPerTab).ToArray() : merged;
        var rows = BacklogSections.Applies(tab) ? BacklogSections.Order(capped) : capped;
        var cursor = truncated ? null : loaded.Page.Next;

        var updated = tab with
        {
            Rows = rows,
            Next = cursor,
            Truncated = truncated,
            Load = LoadState.Idle,
            RequestId = 0,
            IsStale = false,
            LoadedAt = loaded.At,
            Restored = false,
            PendingRows = null,
            ProtectedKeys = [],
            ProtectedRows = [],
            RemovedKeys = [],
            KeyWidth = KeyWidthOf(appended ? tab.KeyWidth : 0, appended ? loaded.Page.Items : merged),
            Failures = 0,
            Total = loaded.Page.Total ?? tab.Total,
            TotalIsApproximate = loaded.Page.Total is null && tab.TotalIsApproximate,
            NextRefreshAt = appended && tab.Failures == 0
                ? tab.NextRefreshAt
                : RefreshSchedule.AfterSuccess(loaded.At),
        };

        var selected = appended && !BacklogSections.Applies(tab) ? updated : KeepSelection(updated);

        // A page narrowed by quick filters is not the board's view, so it never goes to the cache.
        if (!appended && tab.ActiveQuickFilters.Count == 0)
        {
            effects.Add(FirstPageWrite(tab.Definition, tab.Board, combined, loaded.At));
        }

        var recovered = Replace(state, selected);

        if (state.Session.Link is Offline or Rejected)
        {
            recovered = WithLink(recovered, new Online());
        }

        var enforced = EnforceTotal(recovered, effects);
        var current = Find(enforced.Session.Tabs, selected.Id)!;

        // The reply may still leave the end within reach (a tall viewport, or a selection waiting at the last row).
        return WantsNextPage(enforced, current) ? RequestNext(enforced, current, effects) : enforced;
    }

    /// <summary>
    /// The rows plus a further page, without the issues already held: offset paging shifts when issues are created
    /// or moved between two page requests, so a page can repeat a card of the previous one.
    /// </summary>
    private static List<IssueSummary> Append(IReadOnlyList<IssueSummary> rows, IReadOnlyList<IssueSummary> page)
    {
        var known = new HashSet<string>(rows.Count);

        foreach (var row in rows)
        {
            known.Add(row.Key);
        }

        var combined = new List<IssueSummary>(rows.Count + page.Count);

        combined.AddRange(rows);

        foreach (var item in page)
        {
            if (known.Add(item.Key))
            {
                combined.Add(item);
            }
        }

        return combined;
    }

    /// <summary>
    /// Cached rows for a tab that is still loading: shown at once, marked stale, only while no rows are there (a
    /// network reply that came first wins) and the request is still the tab's current one.
    /// </summary>
    private static AppState CacheLoaded(AppState state, ViewCacheLoaded cached)
    {
        var tab = Find(state.Session.Tabs, cached.Tab);

        if (tab is null || tab.RequestId != cached.RequestId || tab.Load is not Loading || tab.Rows.Count > 0)
        {
            return state;
        }

        if (cached.FromScrumBacklog && tab.Definition.Source == ViewSourceKind.Backlog)
        {
            tab = tab with { Definition = tab.Definition with { Source = ViewSourceKind.ScrumBacklog } };
        }

        var rows = BacklogSections.Applies(tab) ? BacklogSections.Order(cached.View.Rows) : cached.View.Rows;

        if (rows.Count == 0)
        {
            return state;
        }

        var shown = tab with
        {
            Rows = rows,
            Next = null,
            KeyWidth = KeyWidthOf(0, rows),
            IsStale = true,
            LoadedAt = cached.View.SavedAt,
            Board = tab.Board ?? cached.View.Board,
        };

        return Replace(state, KeepSelection(shown));
    }

    private static WriteCacheEffect FirstPageWrite(
        ViewDefinition definition,
        BoardConfiguration? board,
        IReadOnlyList<IssueSummary> items,
        DateTimeOffset at)
    {
        var rows = items.Take(ViewPaging.PageSize).ToArray();

        return new WriteCacheEffect(CacheKey.For(definition), new CachedView(rows, board, at));
    }

    private static int KeyWidthOf(int known, IReadOnlyList<IssueSummary> items)
    {
        var width = known;

        for (var index = 0; index < items.Count; index++)
        {
            width = Math.Max(width, items[index].Key.Length);
        }

        return width;
    }

    /// <summary>Over the cap, or exactly at it with more pages behind: the tab stops loading more.</summary>
    private static bool IsOverCap(int count, PageCursor? next)
    {
        return count > ViewPaging.MaxRowsPerTab || (count == ViewPaging.MaxRowsPerTab && next is not null);
    }

    private static AppState LoadFailed(AppState state, ViewLoadFailed failed, List<Effect> effects)
    {
        var tab = Find(state.Session.Tabs, failed.Tab);

        if (tab is null || tab.RequestId != failed.RequestId || tab.Load is not Loading loading)
        {
            return state;
        }

        if (failed.Error.Kind == JiraErrorKind.PageExpired && loading.Reason == LoadReason.NextPage)
        {
            return Restart(state, tab, effects);
        }

        var message = LoadErrorText.For(failed.Error);
        var backsOff = loading.Reason != LoadReason.NextPage;
        var failures = backsOff ? tab.Failures + 1 : tab.Failures;
        var nextRefresh = backsOff ? RefreshSchedule.AfterFailure(failed.At, failures) : tab.NextRefreshAt;

        var failedTab = tab with
        {
            Load = new Failed(failed.Error.Kind, message, loading.Reason),
            RequestId = 0,
            IsStale = tab.Rows.Count > 0,
            PendingRows = null,
            ProtectedKeys = [],
            ProtectedRows = [],
            RemovedKeys = [],
            Failures = failures,
            NextRefreshAt = nextRefresh,
        };

        var link = LinkAfterFailure(state.Session.Link, failed.Error.Kind, failed.At);

        return WithLink(Replace(state, failedTab), link);
    }

    /// <summary>Makes <paramref name="id"/> active and reloads it when it is cold.</summary>
    private static AppState Show(AppState state, TabId id, List<Effect> effects)
    {
        var sequence = state.Sequence;
        var tab = Find(state.Session.Tabs, id)!;
        var shown = tab with
        {
            LastActiveSeq = ++sequence,
            NextRefreshAt = RefreshSchedule.OnActivation(tab.NextRefreshAt, tab.LoadedAt, tab.Failures),
        };

        if (tab.Cold)
        {
            var requestId = ++sequence;

            shown = shown with { Cold = false, Load = new Loading(LoadReason.Open), RequestId = requestId };

            if (tab.ActiveQuickFilters.Count > 0 && !tab.QuickFiltersKnown)
            {
                // A restored tab: the first page needs the filter clauses, so the filters are read first.
                shown = shown with { AwaitingFilters = true };
                effects.Add(new LoadQuickFiltersEffect(id, tab.Definition, false));
            }
            else
            {
                effects.Add(
                    new LoadViewPageEffect(
                        id,
                        tab.Definition,
                        null,
                        requestId,
                        LoadReason.Open,
                        false,
                        QuickFilterJql.For(tab)));
            }
        }

        var session = state.Session with { Active = id };
        var next = Replace(state with { Session = session, Sequence = sequence }, shown);

        // A write marked the shown issue stale while this tab was hidden.
        var reloaded = DetailReducer.NeedsReload(shown) ? DetailReducer.Reload(next, id, effects) : next;

        return EnforceTotal(reloaded, effects);
    }

    /// <summary>Hidden tabs with the lowest last-active stamp give their rows up until the total fits.</summary>
    private static AppState EnforceTotal(AppState state, List<Effect> effects)
    {
        var tabs = state.Session.Tabs;
        var total = tabs.Sum(tab => tab.HeldRows);

        if (total <= ViewPaging.MaxRowsTotal)
        {
            return state;
        }

        var cooled = tabs.ToArray();
        var candidates = Enumerable.Range(0, cooled.Length)
            .Where(index => cooled[index].Id != state.Session.Active && cooled[index].HeldRows > 0)
            .OrderBy(index => cooled[index].LastActiveSeq);

        foreach (var index in candidates)
        {
            if (total <= ViewPaging.MaxRowsTotal)
            {
                break;
            }

            total -= cooled[index].HeldRows;
            cooled[index] = GoCold(cooled[index]);
            effects.Add(new CancelTabEffect(cooled[index].Id));
        }

        return WithSession(state, cooled, state.Session.Active, state.Sequence);
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
