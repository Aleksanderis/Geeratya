using Geeratya.Core.Cache;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Detail;

internal static partial class DetailReducer
{
    private static DetailState ChangedIssue(
        AppState state,
        TabState tab,
        DetailState detail,
        string key,
        Func<DetailState, DetailState> change)
    {
        if (!IsKey(detail, key))
        {
            return detail;
        }

        return Layout(change(detail), DetailSize.Columns(state), DetailSize.ListRows(state, tab));
    }

    private static bool IsKey(DetailState detail, string key)
    {
        return string.Equals(detail.Key, key, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether the shown issue was marked stale by a write and has no reload in flight.</summary>
    public static bool NeedsReload(TabState tab)
    {
        return tab.Detail is [.., { IssueStale: true, IssueRequest: 0, Issue: not null }];
    }

    public static AppState ChildrenLoaded(AppState state, IssueChildrenLoaded loaded)
    {
        return Update(
            state,
            loaded.Tab,
            detail => detail.ChildrenRequest == loaded.RequestId
                ? detail with
                {
                    Children = loaded.Page.Items,
                    ChildrenMore = !loaded.Page.IsLast,
                    ChildrenLoad = LoadState.Idle,
                    ChildrenRequest = 0,
                    LoadedAt = loaded.At,
                }
                : null);
    }

    /// <summary>The edit metadata arrived: the stacked issue of the tab with that key learns which fields are editable.</summary>
    public static AppState EditableLoaded(AppState state, IssueEditableLoaded loaded)
    {
        return Update(
            state,
            loaded.Tab,
            detail => IsKey(detail, loaded.Key) ? detail with { EditableFields = loaded.FieldIds } : null);
    }

    public static AppState CommentsLoaded(AppState state, IssueCommentsLoaded loaded)
    {
        return Update(state, loaded.Tab, detail => AppendComments(detail, loaded));
    }

    public static AppState LinksLoaded(AppState state, IssueRemoteLinksLoaded loaded)
    {
        return Update(
            state,
            loaded.Tab,
            detail => detail.LinksRequest == loaded.RequestId
                ? detail with
                {
                    RemoteLinks = loaded.Links,
                    LinksLoad = LoadState.Idle,
                    LinksRequest = 0,
                    LoadedAt = loaded.At,
                }
                : null);
    }

    public static AppState LoadFailed(AppState state, IssueDetailFailed failed, List<Effect> effects)
    {
        if (failed.Error.Kind is JiraErrorKind.NotFound or JiraErrorKind.Forbidden
            && ViewReducer.Find(state.Session.Tabs, failed.Tab) is { } tab
            && tab.Detail.LastOrDefault(detail => detail.IssueRequest == failed.RequestId) is { } loading)
        {
            // The issue is gone or hidden: its cached copy must not come back on the next open.
            effects.Add(new RemoveIssueCacheEffect(loading.Key));
        }

        var failure = new Failed(failed.Error.Kind, LoadErrorText.For(failed.Error));

        return Update(state, failed.Tab, detail => Fail(detail, failed.RequestId, failure));
    }

    private static AppState MoreComments(AppState state, TabState tab, List<Effect> effects)
    {
        var top = tab.Detail[^1];
        var canLoad = top.CommentsLoad is Failed || (top.CommentsLoad is IdleState && top.NextComments is not null);

        if (!canLoad || top.NextComments is not { } startAt)
        {
            return state;
        }

        var requestId = state.Sequence + 1;
        var loading = top with { CommentsLoad = new Loading(LoadReason.NextPage), CommentsRequest = requestId };
        var laidOut = Layout(loading, DetailSize.Columns(state), DetailSize.ListRows(state, tab));

        effects.Add(new LoadIssueCommentsEffect(tab.Id, top.Key, startAt, requestId, top.CommentsNewestFirst));

        return ViewReducer.Replace(state, ReplaceTop(tab, laidOut)) with { Sequence = requestId };
    }

    /// <summary>The items of a later page that are not shown yet (a comment added elsewhere shifts the offsets).</summary>
    private static IEnumerable<IssueComment> NotLoaded(DetailState detail, IEnumerable<IssueComment> items)
    {
        var known = detail.Comments.Select(comment => comment.Id).ToHashSet();

        return items.Where(comment => !known.Contains(comment.Id));
    }

    private static DetailState? AppendComments(DetailState detail, IssueCommentsLoaded loaded)
    {
        if (detail.CommentsRequest != loaded.RequestId)
        {
            return null;
        }

        var page = loaded.Page;
        var combined = page.StartAt == 0 ? page.Items : [.. detail.Comments, .. NotLoaded(detail, page.Items)];
        var capped = combined.Count >= DetailPaging.MaxComments;
        var kept = capped ? combined.Take(DetailPaging.MaxComments).ToArray() : combined;

        return detail with
        {
            Comments = kept,
            NextComments = capped ? null : page.NextStartAt,
            CommentTotal = page.Total,
            CommentsLoad = LoadState.Idle,
            CommentsRequest = 0,
            LoadedAt = loaded.At,
        };
    }

    private static DetailState? Fail(DetailState detail, long requestId, Failed failure)
    {
        if (detail.IssueRequest == requestId)
        {
            // A cached copy of an issue that is gone or forbidden must not stay on screen.
            return failure.Kind is JiraErrorKind.NotFound or JiraErrorKind.Forbidden
                ? detail with
                {
                    Issue = null,
                    IssueStale = false,
                    IssueSavedAt = null,
                    IssueLoad = failure,
                    IssueRequest = 0,
                }
                : detail with { IssueLoad = failure, IssueRequest = 0, HeldChanges = null };
        }

        if (detail.CommentsRequest == requestId)
        {
            return detail with { CommentsLoad = failure, CommentsRequest = 0 };
        }

        if (detail.ChildrenRequest == requestId)
        {
            return detail with { ChildrenLoad = failure, ChildrenRequest = 0 };
        }

        return detail.LinksRequest == requestId ? detail with { LinksLoad = failure, LinksRequest = 0 } : null;
    }

    private static DetailState? AcceptRequested(DetailState detail, IssueDetailLoaded loaded, long childrenRequest)
    {
        if (detail.IssueRequest != loaded.RequestId)
        {
            return null;
        }

        return AcceptIssue(detail, loaded, childrenRequest);
    }

    private static DetailState AcceptIssue(DetailState detail, IssueDetailLoaded loaded, long childrenRequest)
    {
        return detail with
        {
            Issue = loaded.Issue,
            IssueStale = false,
            IssueSavedAt = null,
            HeldChanges = null,
            IssueLoad = LoadState.Idle,
            IssueRequest = 0,
            LoadedAt = loaded.At,
            Children = null,
            ChildrenMore = false,
            ChildrenLoad = childrenRequest > 0 ? new Loading(LoadReason.Open) : LoadState.Idle,
            ChildrenRequest = childrenRequest,
        };
    }

    /// <summary>
    /// Applies <paramref name="change"/> to the first stacked issue (from the top) that accepts it, then lays that
    /// issue out again. A change that returns null means "not for this issue".
    /// </summary>
    private static AppState Update(AppState state, TabId id, Func<DetailState, DetailState?> change)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, id);

        if (tab is null)
        {
            return state;
        }

        for (var index = tab.Detail.Count - 1; index >= 0; index--)
        {
            if (change(tab.Detail[index]) is not { } changed)
            {
                continue;
            }

            var stack = tab.Detail.ToArray();

            stack[index] = Layout(changed, DetailSize.Columns(state), DetailSize.ListRows(state, tab));

            return ViewReducer.Replace(state, tab with { Detail = stack });
        }

        return state;
    }
}
