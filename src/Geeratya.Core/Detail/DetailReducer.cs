using Geeratya.Core.Cache;
using Geeratya.Core.History;
using Geeratya.Core.Jump;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Detail;

/// <summary>
/// The issue detail part of the view reducer: the per-tab stack of opened issues, their three loads and the
/// scrolling. Pure. A reply is applied only while its request id is the current one of a stacked issue of that tab;
/// anything else (popped issue, closed tab, other site) returns the state unchanged.
/// </summary>
internal static partial class DetailReducer
{
    /// <summary>Enter on a list row opens it; Enter inside a detail loads (or retries) the comments.</summary>
    public static AppState OpenSelected(AppState state, List<Effect> effects)
    {
        var tab = ActiveTab(state);

        if (tab is null)
        {
            return state;
        }

        if (tab.Detail.Count > 0)
        {
            if (TargetOf(tab.Detail[^1]) is { Kind: DetailTargetKind.Issue } target)
            {
                var pushed = Open(state, tab.Id, target.Value, null, effects);

                return pushed == state ? state with { Message = "Too many issues open: press Esc to go back." } : pushed;
            }

            return MoreComments(state, tab, effects);
        }

        if (tab.Rows.Count == 0)
        {
            return state;
        }

        var row = tab.Definition.Layout == ViewLayout.Columns
            ? ColumnsNavigation.SelectedIssue(tab)
            : tab.Rows[Math.Clamp(tab.Selected, 0, tab.Rows.Count - 1)];

        if (row is null)
        {
            return state;
        }

        return Open(state, tab.Id, row.Key, row, effects);
    }

    public static AppState Open(AppState state, TabId id, string key, IssueSummary? row, List<Effect> effects)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, id);

        if (tab is null || tab.Detail.Count >= DetailPaging.MaxStack)
        {
            return state;
        }

        var sequence = state.Sequence;
        var issueRequest = ++sequence;
        var commentsRequest = ++sequence;
        var linksRequest = ++sequence;
        var opened = DetailState.Opened(key, row, issueRequest, commentsRequest, linksRequest) with
        {
            EstimateLabel = tab.Board?.EstimationFieldName,
        };
        var detail = Layout(opened, DetailSize.Columns(state), DetailSize.ListRows(state, tab));

        effects.Add(new LoadIssueDetailEffect(id, key, issueRequest));
        effects.Add(new LoadIssueCommentsEffect(id, key, 0, commentsRequest));
        effects.Add(new LoadRemoteLinksEffect(id, key, linksRequest));

        var next = ViewReducer.Replace(state, tab with { Detail = [.. tab.Detail, detail] }) with { Sequence = sequence };
        var recorded = SourceReducer.Recorded(new ReduceResult(next, []), HistoryKind.Issue, key);

        effects.AddRange(recorded.Effects);

        return recorded.State;
    }

    /// <summary>Esc: pops the top issue; with an empty stack nothing happens.</summary>
    public static AppState Back(AppState state, TabId id)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, id);

        if (tab is null || tab.Detail.Count == 0)
        {
            return state;
        }

        var remaining = tab.Detail.Take(tab.Detail.Count - 1).ToArray();

        var shown = tab with { Detail = remaining };

        return ViewReducer.Replace(state, Resized(shown, DetailSize.Columns(state), DetailSize.ListRows(state, tab)));
    }

    /// <summary>Closes the overlay: the whole issue stack of the tab is dropped, the view is as it was left.</summary>
    public static AppState Close(AppState state, TabId id)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, id);

        return tab is null || tab.Detail.Count == 0 ? state : ViewReducer.Replace(state, tab with { Detail = [] });
    }

    public static bool CanGoBack(AppState state)
    {
        return ActiveTab(state) is { Detail.Count: > 0 };
    }

    /// <summary>
    /// The issue arrived. An issue of a type with children (hierarchy level 1 or above) starts loading them now,
    /// one level only: a grandchild is not returned by <c>parent = KEY</c> (<c>docs/jira-layer.md</c>, "Hierarchy").
    /// An issue with sub-tasks loads them the same way, because Jira lists a sub-task without its assignee.
    /// </summary>
    public static AppState IssueLoaded(AppState state, IssueDetailLoaded loaded, List<Effect> effects)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, loaded.Tab);

        if (tab is null || !tab.Detail.Any(detail => detail.IssueRequest == loaded.RequestId))
        {
            return state;
        }

        var wantsChildren = loaded.Issue.IssueType.HierarchyLevel >= DetailPaging.ParentLevel
            || loaded.Issue.Subtasks.Count > 0;
        var childrenRequest = wantsChildren ? state.Sequence + 1 : 0;

        // Fields that a running or just ended write changed are kept (the reply may predate the write).
        var current = tab.Detail.Last(detail => detail.IssueRequest == loaded.RequestId);
        var issue = WriteMerge.KeepHeldFields(state, current, loaded.Issue);

        var cached = new CachedIssue(WriteMerge.KeepEndedFields(current, loaded.Issue), loaded.At);

        effects.Add(new WriteIssueCacheEffect(cached));

        var accepted = loaded with { Issue = issue };
        var next = JumpReducer.Remember(
            Update(state, loaded.Tab, detail => AcceptRequested(detail, accepted, childrenRequest)),
            loaded.Issue.Key);

        if (!wantsChildren)
        {
            return next;
        }

        effects.Add(new LoadIssueChildrenEffect(loaded.Tab, loaded.Issue.Key, childrenRequest));

        return next with { Sequence = childrenRequest };
    }

    /// <summary>
    /// The cached issue is shown (marked stale) while its request is still in flight; once an issue is shown, or the
    /// request ended, the cache reply is dropped.
    /// </summary>
    public static AppState IssueCached(AppState state, IssueDetailCached cached)
    {
        return Update(
            state,
            cached.Tab,
            detail => detail.IssueRequest == cached.RequestId && detail.Issue is null
                ? detail with { Issue = cached.Cached.Detail, IssueStale = true, IssueSavedAt = cached.Cached.SavedAt }
                : null);
    }

    /// <summary>
    /// Reloads the shown issue of a tab after a write on it: the shown copy stays (marked stale)
    /// and a new request replaces any older one, so a reply that predates the write is dropped.
    /// </summary>
    public static AppState Reload(AppState state, TabId id, List<Effect> effects)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, id);

        if (tab is null || tab.Detail.Count == 0)
        {
            return state;
        }

        var top = tab.Detail[^1];
        var requestId = state.Sequence + 1;

        var loading = top with
        {
            IssueStale = true,
            IssueSavedAt = top.IssueSavedAt ?? top.LoadedAt,
            IssueLoad = new Loading(LoadReason.Refresh, true),
            IssueRequest = requestId,
            HeldChanges = null,
        };

        effects.Add(new LoadIssueDetailEffect(id, top.Key, requestId));

        return ViewReducer.Replace(state, ReplaceTop(tab, loading)) with { Sequence = requestId };
    }

    /// <summary>
    /// Applies <paramref name="change"/> to every stacked issue with <paramref name="key"/> in every tab and lays it
    /// out again (a comment was added, edited or deleted: the same issue may be open in several tabs).
    /// </summary>
    public static AppState ChangeIssues(AppState state, string key, Func<DetailState, DetailState> change)
    {
        var next = state;

        foreach (var tab in state.Session.Tabs)
        {
            if (!tab.Detail.Any(detail => IsKey(detail, key)))
            {
                continue;
            }

            var stack = tab.Detail.Select(detail => ChangedIssue(state, tab, detail, key, change)).ToArray();

            next = ViewReducer.Replace(next, tab with { Detail = stack });
        }

        return next;
    }

    /// <summary>
    /// Loads the first comment page of the top-most stacked issue <paramref name="key"/> of a tab again, replacing the
    /// list when it arrives (the reply of a write did not carry the comment).
    /// </summary>
    public static AppState ReloadComments(AppState state, TabId id, string key, List<Effect> effects)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, id);
        var index = tab is null ? -1 : tab.Detail.ToList().FindLastIndex(detail => IsKey(detail, key));

        if (tab is null || index < 0)
        {
            return state;
        }

        var requestId = state.Sequence + 1;
        var stack = tab.Detail.ToArray();

        stack[index] = Layout(
            stack[index] with { CommentsLoad = new Loading(LoadReason.Refresh), CommentsRequest = requestId },
            DetailSize.Columns(state),
            DetailSize.ListRows(state, tab));
        effects.Add(new LoadIssueCommentsEffect(id, key, 0, requestId));

        return ViewReducer.Replace(state, tab with { Detail = stack }) with { Sequence = requestId };
    }
}
