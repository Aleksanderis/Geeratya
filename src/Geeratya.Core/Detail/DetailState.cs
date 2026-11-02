using Geeratya.Core.Views;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.RemoteLinks.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Detail;

/// <summary>
/// One opened issue on a tab's stack. Its three parts load in parallel and each has its own request id: a reply
/// is applied only while its id is the part's current one (<c>docs/architecture.md</c>, "Views and tabs").
/// </summary>
/// <param name="Key">The issue key.</param>
/// <param name="Row">The list row it was opened from, shown in the header until the issue arrives; null when opened by key.</param>
/// <param name="Issue">The loaded issue, null until it arrives.</param>
/// <param name="IssueLoad">Whether the issue is loading or failed.</param>
/// <param name="IssueRequest">The id of the issue request in flight, 0 when none.</param>
/// <param name="Comments">The comments loaded so far, oldest first.</param>
/// <param name="NextComments">The offset of the next comment page, null when the last page is loaded.</param>
/// <param name="CommentTotal">The number of comments Jira reports, when it does.</param>
/// <param name="CommentsLoad">Whether a comment page is loading or failed.</param>
/// <param name="CommentsRequest">The id of the comment request in flight, 0 when none.</param>
/// <param name="RemoteLinks">The remote links, null until they arrive (drawn by the relations issue, #88).</param>
/// <param name="LinksLoad">Whether the remote links are loading or failed.</param>
/// <param name="LinksRequest">The id of the remote link request in flight, 0 when none.</param>
/// <param name="LoadedAt">When the last reply arrived; the reference for relative times.</param>
/// <param name="Scroll">The first visible document line.</param>
/// <param name="Document">The laid-out lines for the last known width, null before the first layout.</param>
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
    DateTimeOffset? LoadedAt,
    int Scroll,
    DetailDocument? Document)
{
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
            0,
            null);
    }
}
