using System.Globalization;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Issues;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Comments;

/// <summary>
/// Reads the comments of one issue, one offset page at a time, oldest first. Reads only; posting is a separate
/// write. A missing or hidden issue is a <see cref="JiraErrorKind.NotFound"/> ("not found or no access").
/// </summary>
public sealed class CommentClient(JiraTransport transport)
{
    /// <summary>Default and largest page size requested.</summary>
    public const int MaxPageSize = 100;

    private const string IssuePath = "/rest/api/3/issue";

    /// <summary>
    /// Loads the page starting at <paramref name="startAt"/>; continue with <see cref="OffsetPage{T}.NextStartAt"/>
    /// until it is null.
    /// </summary>
    public async Task<JiraResult<OffsetPage<IssueComment>>> GetCommentsAsync(
        string key,
        int startAt,
        CancellationToken cancellationToken,
        int pageSize = MaxPageSize,
        JiraPriority priority = JiraPriority.User)
    {
        if (!IssueUrls.IsValidKey(key))
        {
            return JiraResults.Failure<OffsetPage<IssueComment>>(
                new JiraError(JiraErrorKind.NotFound, null, "Not found or no access."));
        }

        var start = Math.Max(startAt, 0);
        var size = Math.Clamp(pageSize, 1, MaxPageSize);
        var url = $"{IssuePath}/{key}/comment?orderBy=created&startAt={Number(start)}&maxResults={Number(size)}";

        var result = await transport.SendAsync(
            JiraRequest.Get(url) with { Priority = priority },
            CommentJsonContext.Default.Page,
            cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(CommentMapper.MapPage(result.Value, start, size))
            : JiraResults.Failure<OffsetPage<IssueComment>>(result.Error);
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
