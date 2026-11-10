using Geeratya.Core.Backlog;
using Geeratya.Core.Cache;
using Geeratya.Core.Detail;
using Geeratya.Core.State;
using Geeratya.Core.Writes;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Views;

public static partial class ViewReducer
{
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
            // A refresh brings fresh sub-task stubs without assignees: keep those read for the expanded cards (#911).
            Rows = appended ? rows : SubtaskAssigneeReducer.CarryOver(tab.Rows, rows, tab.ExpandedSubtasks),
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
}
