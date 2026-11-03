using Geeratya.Core.Cache;
using Geeratya.Core.History;
using Geeratya.Core.Jump;
using Geeratya.Core.Lists;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;

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
        var detail = Layout(opened, state.ViewportColumns, tab.VisibleRows);

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
            state.ViewportColumns,
            tab.VisibleRows);
        effects.Add(new LoadIssueCommentsEffect(id, key, 0, requestId));

        return ViewReducer.Replace(state, tab with { Detail = stack }) with { Sequence = requestId };
    }

    private static DetailState ChangedIssue(
        AppState state,
        TabState tab,
        DetailState detail,
        string key,
        Func<DetailState, DetailState> change)
    {
        return IsKey(detail, key) ? Layout(change(detail), state.ViewportColumns, tab.VisibleRows) : detail;
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

    /// <summary>
    /// Moves the cursor of the top issue of the active tab by line or page; the window follows it, scrolling only
    /// when the cursor would leave it.
    /// </summary>
    public static AppState MoveCursor(AppState state, TabState tab, ListMove move)
    {
        var top = tab.Detail[^1];
        var viewport = DetailLayout.ViewportRows(tab.VisibleRows);

        if (top.Document is not { } document)
        {
            return state;
        }

        var target = move switch
        {
            ListMove.Down => document.StopAfter(top.Cursor),
            ListMove.Up => document.StopBefore(top.Cursor),
            ListMove.PageDown => document.Snap(top.Cursor + Math.Max(1, viewport / 2)),
            ListMove.PageUp => document.StopBefore(top.Cursor - Math.Max(1, viewport / 2) + 1),
            ListMove.Top => document.Snap(0),
            _ => document.Stops.Count == 0 ? 0 : document.Stops[^1],
        };

        return ViewReducer.Replace(state, ReplaceTop(tab, WithCursor(top, target, viewport)));
    }

    /// <summary>
    /// A click: puts the cursor of the top issue on the document line shown at <paramref name="bodyRow"/> (the
    /// breadcrumb row is 0), without scrolling. Null for rows outside the document.
    /// </summary>
    public static AppState? PlaceCursor(AppState state, TabState tab, int bodyRow)
    {
        var top = tab.Detail[^1];
        var line = top.Scroll + bodyRow - DetailLayout.HeaderRows;

        if (top.Document is not { } document || bodyRow < DetailLayout.HeaderRows || line >= document.Lines.Count)
        {
            return null;
        }

        return ViewReducer.Replace(state, ReplaceTop(tab, top with { Cursor = document.Snap(line) }));
    }

    /// <summary>
    /// Puts the cursor on <paramref name="line"/> and scrolls so that the panel edge above it is the first visible
    /// line when the document is long enough (a section jump shows the section from its top).
    /// </summary>
    public static AppState JumpTo(AppState state, TabState tab, int line)
    {
        var top = tab.Detail[^1];
        var lines = top.Document?.Lines.Count ?? 0;
        var viewport = DetailLayout.ViewportRows(tab.VisibleRows);
        var cursor = top.Document?.Snap(Math.Clamp(line, 0, Math.Max(0, lines - 1))) ?? 0;
        var scroll = Math.Clamp(cursor - 1, 0, Math.Max(0, lines - viewport));

        return ViewReducer.Replace(state, ReplaceTop(tab, top with { Cursor = cursor, Scroll = scroll }));
    }

    private static DetailState WithCursor(DetailState detail, int target, int viewport)
    {
        var lines = detail.Document?.Lines.Count ?? 0;
        var cursor = detail.Document?.Snap(Math.Clamp(target, 0, Math.Max(0, lines - 1))) ?? 0;
        var scroll = detail.Scroll;

        if (detail.Document is { Stops.Count: > 0 } stops && cursor == stops.Stops[^1])
        {
            scroll = lines;
        }
        else if (detail.Document is { Stops.Count: > 0 } first && cursor == first.Stops[0])
        {
            scroll = 0;
        }
        else if (cursor < scroll)
        {
            scroll = cursor;
        }
        else if (cursor >= scroll + viewport)
        {
            scroll = cursor - viewport + 1;
        }

        return detail with { Cursor = cursor, Scroll = Math.Clamp(scroll, 0, Math.Max(0, lines - viewport)) };
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

    /// <summary>What Enter opens on the cursor line of <paramref name="detail"/>, null on a plain line.</summary>
    public static DetailTarget? TargetOf(DetailState detail)
    {
        var targets = detail.Document?.Targets;

        if (targets is null || detail.Cursor < 0 || detail.Cursor >= targets.Count)
        {
            return null;
        }

        return targets[detail.Cursor];
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

            stack[index] = Layout(changed, state.ViewportColumns, tab.VisibleRows);

            return ViewReducer.Replace(state, tab with { Detail = stack });
        }

        return state;
    }

    /// <summary>
    /// The board configuration arrived after issues were opened: the open details that fell back to the generic
    /// estimate label take the board's name for it, without reopening (#470).
    /// </summary>
    public static TabState WithEstimateLabel(TabState tab, int columns)
    {
        var label = tab.Board?.EstimationFieldName;

        if (label is null || tab.Detail.All(detail => detail.EstimateLabel is not null))
        {
            return tab;
        }

        var stack = tab.Detail
            .Select(detail => detail.EstimateLabel is null
                ? Layout(detail with { EstimateLabel = label }, columns, tab.VisibleRows)
                : detail)
            .ToArray();

        return tab with { Detail = stack };
    }

    private static DetailState Layout(DetailState detail, int columns, int listRows)
    {
        var width = columns > 0 ? columns : DetailLayout.DefaultWidth;

        var document = IssueDocumentBuilder.Build(detail, width);
        var anchored = detail.Document is { } old && old.Width == width ? Anchored(detail, old, document) : detail.Cursor;

        return Clamp(detail with { Document = document, Cursor = anchored }, listRows);
    }

    /// <summary>
    /// The cursor line in <paramref name="document"/> that shows the same row as the cursor did in
    /// <paramref name="old"/> (same target, same offset inside a wrapped row), so data arriving above the cursor does
    /// not move it onto another row. A plain line keeps its line number.
    /// </summary>
    private static int Anchored(DetailState detail, DetailDocument old, DetailDocument document)
    {
        if (detail.Cursor < 0 || detail.Cursor >= old.Targets.Count || old.Targets[detail.Cursor] is not { } target)
        {
            return detail.Cursor;
        }

        var start = detail.Cursor;

        while (start > 0 && Equals(old.Targets[start - 1], target))
        {
            start--;
        }

        for (var line = 0; line < document.Targets.Count; line++)
        {
            if (Equals(document.Targets[line], target))
            {
                return Math.Min(line + detail.Cursor - start, document.Targets.Count - 1);
            }
        }

        return detail.Cursor;
    }

    private static DetailState Clamp(DetailState detail, int listRows)
    {
        var lines = detail.Document?.Lines.Count ?? 0;
        var viewport = DetailLayout.ViewportRows(listRows);
        var scrolled = detail with { Scroll = Math.Clamp(detail.Scroll, 0, Math.Max(0, lines - viewport)) };

        return WithCursor(scrolled, detail.Cursor, viewport);
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
