using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.RemoteLinks.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Detail;

/// <summary>
/// One opened issue on a tab's stack. Its three parts load in parallel and each has its own request id: a reply
/// is applied only while its id is the part's current one (<c>docs/internal/architecture.md</c>, "Views and tabs").
/// </summary>
/// <param name="Key">The issue key.</param>
/// <param name="Row">
/// The list row it was opened from, shown in the header until the issue arrives; null when opened by key.
/// </param>
/// <param name="Issue">The loaded issue, null until it arrives.</param>
/// <param name="IssueLoad">Whether the issue is loading or failed.</param>
/// <param name="IssueRequest">The id of the issue request in flight, 0 when none.</param>
/// <param name="Comments">The comments loaded so far, newest first.</param>
/// <param name="NextComments">The offset of the next comment page, null when the last page is loaded.</param>
/// <param name="CommentTotal">The number of comments Jira reports, when it does.</param>
/// <param name="CommentsLoad">Whether a comment page is loading or failed.</param>
/// <param name="CommentsRequest">The id of the comment request in flight, 0 when none.</param>
/// <param name="RemoteLinks">The remote links, null until they arrive.</param>
/// <param name="LinksLoad">Whether the remote links are loading or failed.</param>
/// <param name="LinksRequest">The id of the remote link request in flight, 0 when none.</param>
/// <param name="Children">
/// The children of the issue (<c>parent = KEY</c>), null until they arrive or when the issue type has no children.
/// </param>
/// <param name="ChildrenMore">Whether the children are cut at the first page.</param>
/// <param name="ChildrenLoad">Whether the children are loading or failed.</param>
/// <param name="ChildrenRequest">The id of the children request in flight, 0 when none.</param>
/// <param name="LoadedAt">When the last reply arrived; the reference for relative times.</param>
/// <param name="Cursor">The document line the cursor is on; Enter and the open keys act on it.</param>
/// <param name="Scroll">The first visible document line; follows the cursor.</param>
/// <param name="Document">The laid-out lines for the last known width, null before the first layout.</param>
/// <param name="IssueStale">The shown issue came from the cache; the network reply replaces it.</param>
/// <param name="HeldChanges">
/// What writes that ended while this issue's request was in flight changed; the reply keeps these fields.
/// </param>
/// <param name="IssueSavedAt">When the cached issue was loaded from Jira; the age the STALE banner shows.</param>
public sealed record DetailState(
    string Key,
    IssueSummary? Row,
    IssueDetail? Issue,
    LoadState IssueLoad,
    long IssueRequest,
    IReadOnlyList<IssueComment> Comments,
    int? NextComments,
    int? CommentTotal,
    LoadState CommentsLoad,
    long CommentsRequest,
    IReadOnlyList<RemoteLink>? RemoteLinks,
    LoadState LinksLoad,
    long LinksRequest,
    IReadOnlyList<IssueSummary>? Children,
    bool ChildrenMore,
    LoadState ChildrenLoad,
    long ChildrenRequest,
    DateTimeOffset? LoadedAt,
    int Cursor,
    int Scroll,
    DetailDocument? Document,
    bool IssueStale = false,
    DateTimeOffset? IssueSavedAt = null,
    IReadOnlyList<RowChange>? HeldChanges = null)
{
    /// <summary>The board's name for the estimate field (for example "Story Points"); null shows "Estimate".</summary>
    public string? EstimateLabel { get; init; }

    /// <summary>
    /// The long texts shown in full: <c>description</c> or <c>comment:ID</c>; every other long text starts cut short.
    /// </summary>
    public IReadOnlyList<string> Expanded { get; init; } = [];

    /// <summary>
    /// The ids of the fields the account may edit on this issue (edit metadata), null until they arrive or when the
    /// load failed. The Details pane shows the dropdown mark only for a field listed here.
    /// </summary>
    public IReadOnlyList<string>? EditableFields { get; init; }

    /// <summary>
    /// The cursor line in the Details sidebar (<see cref="DetailDocument.Side"/>) while the sidebar has the focus;
    /// null while the main column has it.
    /// </summary>
    public int? SideCursor { get; init; }

    /// <summary>The first content row of the Comments pane that is shown (its inner scroll).</summary>
    public int CommentsOffset { get; init; }

    /// <summary>The first line of the cut Description that is shown (its inner scroll, #729).</summary>
    public int DescriptionOffset { get; init; }

    /// <summary>
    /// The first content row of the zoomed pane that is shown (its inner scroll, #740); Comments and the cut
    /// Description keep their own offsets. Reset when the zoom changes.
    /// </summary>
    public int ZoomOffset { get; init; }

    /// <summary>
    /// The pane shown zoomed (a <see cref="PaneIds"/> member), null when the panes show together. Kept with the
    /// issue on the tab's stack, so it stays while the tab is open (#705).
    /// </summary>
    public string? Zoomed { get; init; }

    /// <summary>
    /// Where the line selection of the main column started (<c>v</c>, <see cref="YankAnchor"/>), null without one.
    /// A new layout of the document (data arrived, a text expanded or cut, a resize) ends it, because its rows moved.
    /// </summary>
    public YankAnchor? YankAnchor { get; init; }

    /// <summary>An issue with all three parts loading under the given request ids.</summary>
    public static DetailState Opened(
        string key,
        IssueSummary? row,
        long issueRequest,
        long commentsRequest,
        long linksRequest)
    {
        return new DetailState(
            key,
            row,
            null,
            new Loading(LoadReason.Open),
            issueRequest,
            [],
            0,
            null,
            new Loading(LoadReason.Open),
            commentsRequest,
            null,
            new Loading(LoadReason.Open),
            linksRequest,
            null,
            false,
            LoadState.Idle,
            0,
            null,
            0,
            0,
            null);
    }
}
