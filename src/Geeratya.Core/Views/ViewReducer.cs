using Geeratya.Core.State;
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
    private static readonly IReadOnlyList<Effect> _noEffects = [];

    public static ReduceResult Reduce(AppState state, ViewCommand command)
    {
        var effects = new List<Effect>();

        var next = command switch
        {
            ViewOpenRequested open => Open(state, open.Definition, effects),
            TabActivated activated => Activate(state, activated.Tab, effects),
            TabClosed closed => Close(state, closed.Tab, effects),
            ViewLayoutChanged changed => ChangeLayout(state, changed),
            ViewportChanged viewport => ChangeViewport(state, viewport.Rows),
            NextPageRequested requested => NextPage(state, requested.Tab, effects),
            SelectionMoved moved => MoveSelection(state, moved, effects),
            ViewPageLoaded loaded => PageLoaded(state, loaded, effects),
            ViewLoadFailed failed => LoadFailed(state, failed, effects),
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

    private static AppState ChangeLayout(AppState state, ViewLayoutChanged changed)
    {
        var tab = Find(state.Session.Tabs, changed.Tab);

        if (tab is null || tab.Definition.Layout == changed.Layout)
        {
            return state;
        }

        return Replace(state, tab with { Definition = tab.Definition with { Layout = changed.Layout } });
    }

    private static AppState ChangeViewport(AppState state, int rows)
    {
        var height = Math.Max(0, rows);

        if (state.ViewportRows == height && state.Session.Tabs.All(tab => tab.VisibleRows == height))
        {
            return state;
        }

        var tabs = state.Session.Tabs.Select(tab => tab with { VisibleRows = height }).ToArray();

        return WithSession(state, tabs, state.Session.Active, state.Sequence) with { ViewportRows = height };
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

        effects.Add(new LoadViewPageEffect(tab.Id, tab.Definition, tab.Next, requestId, LoadReason.NextPage, false));

        return Replace(state, loading) with { Sequence = requestId };
    }

    /// <summary>Within one screen of the last loaded row (the last row itself counts when the height is unknown).</summary>
    private static bool IsNearEnd(TabState tab)
    {
        return tab.Selected >= tab.Rows.Count - Math.Max(1, tab.VisibleRows);
    }

    private static AppState MoveSelection(AppState state, SelectionMoved moved, List<Effect> effects)
    {
        var tab = Find(state.Session.Tabs, moved.Tab);

        if (tab is null || tab.Rows.Count == 0)
        {
            return state;
        }

        var index = Math.Clamp(moved.Index, 0, tab.Rows.Count - 1);
        var selected = tab with { Selected = index, SelectedKey = tab.Rows[index].Key };
        var next = Replace(state, selected);

        return state.Session.Active == selected.Id && IsNearEnd(selected) ? RequestNext(next, selected, effects) : next;
    }

    /// <summary>
    /// A continued load came back with a rejected page token: start again at the first page, keeping the rows on
    /// screen and the selection by issue key, instead of showing an error.
    /// </summary>
    private static AppState Restart(AppState state, TabState tab, List<Effect> effects)
    {
        var requestId = state.Sequence + 1;
        var loading = tab with { Load = new Loading(LoadReason.Retry), RequestId = requestId, Next = null };

        effects.Add(new LoadViewPageEffect(tab.Id, tab.Definition, null, requestId, LoadReason.Retry, false));

        return Replace(state, loading) with { Sequence = requestId };
    }

    private static AppState PageLoaded(AppState state, ViewPageLoaded loaded, List<Effect> effects)
    {
        var tab = Find(state.Session.Tabs, loaded.Tab);

        if (tab is null || tab.RequestId != loaded.RequestId || tab.Load is not Loading loading)
        {
            return state;
        }

        var appended = loading.Reason == LoadReason.NextPage;
        var combined = appended ? [.. tab.Rows, .. loaded.Page.Items] : loaded.Page.Items;
        var truncated = IsOverCap(combined.Count, loaded.Page.Next);
        var rows = truncated ? combined.Take(ViewPaging.MaxRowsPerTab).ToArray() : combined;
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
            PendingRows = null,
        };

        var selected = appended ? updated : KeepSelection(updated);

        var enforced = EnforceTotal(Replace(state, selected), effects);
        var current = Find(enforced.Session.Tabs, selected.Id)!;

        // The reply may still leave the end within reach (a tall viewport, or a selection waiting at the last row).
        return enforced.Session.Active == current.Id && IsNearEnd(current)
            ? RequestNext(enforced, current, effects)
            : enforced;
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

        return Replace(
            state,
            tab with
            {
                Load = new Failed(failed.Error.Kind, message),
                RequestId = 0,
                IsStale = tab.Rows.Count > 0,
                PendingRows = null,
            });
    }

    /// <summary>Makes <paramref name="id"/> active and reloads it when it is cold.</summary>
    private static AppState Show(AppState state, TabId id, List<Effect> effects)
    {
        var sequence = state.Sequence;
        var tab = Find(state.Session.Tabs, id)!;
        var shown = tab with { LastActiveSeq = ++sequence };

        if (tab.Cold)
        {
            var requestId = ++sequence;

            shown = shown with { Cold = false, Load = new Loading(LoadReason.Open), RequestId = requestId };
            effects.Add(new LoadViewPageEffect(id, tab.Definition, null, requestId, LoadReason.Open, false));
        }

        var session = state.Session with { Active = id };
        var next = Replace(state with { Session = session, Sequence = sequence }, shown);

        return EnforceTotal(next, effects);
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
            Scroll = 0,
            RequestId = 0,
            Load = LoadState.Idle,
            Cold = true,
        };
    }

    /// <summary>After rows were replaced: selection follows its issue key, else the index is clamped.</summary>
    private static TabState KeepSelection(TabState tab)
    {
        var rows = tab.Rows;

        if (rows.Count == 0)
        {
            return tab with { Selected = 0, SelectedKey = null, Scroll = 0 };
        }

        var found = FindKey(rows, tab.SelectedKey);
        var selected = found >= 0 ? found : Math.Clamp(tab.Selected, 0, rows.Count - 1);

        return tab with
        {
            Selected = selected,
            SelectedKey = rows[selected].Key,
            Scroll = Math.Clamp(tab.Scroll, 0, rows.Count - 1),
        };
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

    private static AppState Replace(AppState state, TabState tab)
    {
        var tabs = state.Session.Tabs.Select(existing => existing.Id == tab.Id ? tab : existing).ToArray();

        return state with { Session = state.Session with { Tabs = tabs } };
    }

    private static TabState? Find(IReadOnlyList<TabState> tabs, TabId id)
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
