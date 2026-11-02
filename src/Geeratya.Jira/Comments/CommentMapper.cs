using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Jira.Comments;

/// <summary>Maps a comment page to <see cref="OffsetPage{T}"/>. Pure and tolerant: nothing here throws.</summary>
internal static class CommentMapper
{
    public static OffsetPage<IssueComment> MapPage(CommentWire.Page page, int requestedStartAt, int requestedSize)
    {
        var items = new List<IssueComment>(page.Comments?.Count ?? 0);

        foreach (var comment in page.Comments ?? [])
        {
            if (comment is not null)
            {
                items.Add(Map(comment));
            }
        }

        var startAt = page.StartAt ?? requestedStartAt;
        var total = page.Total;

        // An empty page also ends the walk, so a total that is too large cannot loop forever.
        var isLast = items.Count == 0 || (total is { } known && startAt + items.Count >= known);

        return new OffsetPage<IssueComment>(items, startAt, page.MaxResults ?? requestedSize, total, isLast);
    }

    private static IssueComment Map(CommentWire.Comment comment) =>
        new(
            comment.Id ?? string.Empty,
            comment.Author is { } author
                ? new IssueAssignee(author.AccountId ?? string.Empty, author.DisplayName ?? string.Empty)
                : null,
            comment.Body is { } body ? AdfParser.Parse(body) : AdfParser.Parse((string?)null),
            IssueSummaryMapper.ParseTimestamp(comment.Created),
            IssueSummaryMapper.ParseTimestamp(comment.Updated));
}
