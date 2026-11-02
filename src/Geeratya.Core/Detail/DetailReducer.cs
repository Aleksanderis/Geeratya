using Geeratya.Core.Lists;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Detail;

/// <summary>
/// The issue detail part of the view reducer: the per-tab stack of opened issues, their three loads and the
/// scrolling. Pure. A reply is applied only while its request id is the current one of a stacked issue of that tab;
/// anything else (popped issue, closed tab, other site) returns the state unchanged.
/// </summary>
internal static class DetailReducer
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
            return MoreComments(state, tab, effects);
        }

        if (tab.Rows.Count == 0)
        {
            return state;
        }

        var row = tab.Rows[Math.Clamp(tab.Selected, 0, tab.Rows.Count - 1)];

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
        var opened = DetailState.Opened(key, row, issueRequest, commentsRequest, linksRequest);
        var detail = Layout(opened, state.ViewportColumns, tab.VisibleRows);

        effects.Add(new LoadIssueDetailEffect(id, key, issueRequest));
        effects.Add(new LoadIssueCommentsEffect(id, key, 0, commentsRequest));
        effects.Add(new LoadRemoteLinksEffect(id, key, linksRequest));

        return ViewReducer.Replace(state, tab with { Detail = [.. tab.Detail, detail] }) with { Sequence = sequence };
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

        return ViewReducer.Replace(state, Resized(tab with { Detail = remaining }, state.ViewportColumns));
    }

    public static bool CanGoBack(AppState state)
    {
        return ActiveTab(state) is { Detail.Count: > 0 };
    }

    /// <summary>
    /// The issue arrived. An issue of a type with children (hierarchy level 1 or above) starts loading them now,
    /// one level only: a grandchild is not returned by <c>parent = KEY</c> (<c>docs/jira-layer.md</c>, "Hierarchy").
    /// </summary>
    public static AppState IssueLoaded(AppState state, IssueDetailLoaded loaded, List<Effect> effects)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, loaded.Tab);

        if (tab is null || !tab.Detail.Any(detail => detail.IssueRequest == loaded.RequestId))
        {
            return state;
        }

        var wantsChildren = loaded.Issue.IssueType.HierarchyLevel >= DetailPaging.ParentLevel;
        var childrenRequest = wantsChildren ? state.Sequence + 1 : 0;

        var next = Update(
            state,
            loaded.Tab,
            detail => detail.IssueRequest == loaded.RequestId ? AcceptIssue(detail, loaded, childrenRequest) : null);

        if (!wantsChildren)
        {
            return next;
        }

        effects.Add(new LoadIssueChildrenEffect(loaded.Tab, loaded.Issue.Key, childrenRequest));

        return next with { Sequence = childrenRequest };
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

    public static AppState LoadFailed(AppState state, IssueDetailFailed failed)
    {
        var failure = new Failed(failed.Error.Kind, LoadErrorText.For(failed.Error));

        return Update(state, failed.Tab, detail => Fail(detail, failed.RequestId, failure));
    }

    /// <summary>
    /// Scrolls the top issue of the active tab by line or page; the document, not the selection, moves.
    /// </summary>
    public static AppState Scroll(AppState state, TabState tab, ListMove move)
    {
        var top = tab.Detail[^1];
        var lines = top.Document?.Lines.Count ?? 0;
        var viewport = DetailLayout.ViewportRows(tab.VisibleRows);
        var last = Math.Max(0, lines - viewport);

        var target = move switch
        {
            ListMove.Down => top.Scroll + 1,
            ListMove.Up => top.Scroll - 1,
            ListMove.PageDown => top.Scroll + Math.Max(1, viewport / 2),
            ListMove.PageUp => top.Scroll - Math.Max(1, viewport / 2),
            ListMove.Top => 0,
            _ => last,
        };

        var scrolled = top with { Scroll = Math.Clamp(target, 0, last) };

        return ViewReducer.Replace(state, ReplaceTop(tab, scrolled));
    }

    /// <summary>
    /// After a viewport change: the top issue of every tab is laid out for the new width and re-clamped.
    /// </summary>
    public static TabState Resized(TabState tab, int columns)
    {
        if (tab.Detail.Count == 0)
        {
            return tab;
        }

        var top = tab.Detail[^1];
        var width = columns > 0 ? columns : DetailLayout.DefaultWidth;
        var sameWidth = top.Document?.Width == width;
        var resized = sameWidth ? Clamp(top, tab.VisibleRows) : Layout(top, columns, tab.VisibleRows);

        return ReplaceTop(tab, resized);
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
        var laidOut = Layout(loading, state.ViewportColumns, tab.VisibleRows);

        effects.Add(new LoadIssueCommentsEffect(tab.Id, top.Key, startAt, requestId));

        return ViewReducer.Replace(state, ReplaceTop(tab, laidOut)) with { Sequence = requestId };
    }

    private static DetailState? AppendComments(DetailState detail, IssueCommentsLoaded loaded)
    {
        if (detail.CommentsRequest != loaded.RequestId)
        {
            return null;
        }

        var page = loaded.Page;
        var combined = page.StartAt == 0 ? page.Items : [.. detail.Comments, .. page.Items];
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
            return detail with { IssueLoad = failure, IssueRequest = 0 };
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

    private static DetailState AcceptIssue(DetailState detail, IssueDetailLoaded loaded, long childrenRequest)
    {
        return detail with
        {
            Issue = loaded.Issue,
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

            stack[index] = Layout(changed, state.ViewportColumns, tab.VisibleRows);

            return ViewReducer.Replace(state, tab with { Detail = stack });
        }

        return state;
    }

    private static DetailState Layout(DetailState detail, int columns, int listRows)
    {
        var width = columns > 0 ? columns : DetailLayout.DefaultWidth;

        return Clamp(detail with { Document = IssueDocumentBuilder.Build(detail, width) }, listRows);
    }

    private static DetailState Clamp(DetailState detail, int listRows)
    {
        var lines = detail.Document?.Lines.Count ?? 0;
        var last = Math.Max(0, lines - DetailLayout.ViewportRows(listRows));

        return detail with { Scroll = Math.Clamp(detail.Scroll, 0, last) };
    }

    private static TabState ReplaceTop(TabState tab, DetailState top)
    {
        var stack = tab.Detail.ToArray();

        stack[^1] = top;

        return tab with { Detail = stack };
    }

    private static TabState? ActiveTab(AppState state)
    {
        return state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;
    }
}
