using Geeratya.Jira.Issues;
using Geeratya.Jira.RemoteLinks.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.RemoteLinks;

/// <summary>
/// Reads the remote links of one issue (Confluence pages, web links). They are not part of the issue fields and
/// not paged. Reads only. A missing or hidden issue is a <see cref="JiraErrorKind.NotFound"/>.
/// </summary>
public sealed class RemoteLinkClient(JiraTransport transport)
{
    private const string IssuePath = "/rest/api/3/issue";

    /// <summary>Loads the links of an issue. Entries without a target URL are skipped.</summary>
    public async Task<JiraResult<IReadOnlyList<RemoteLink>>> GetRemoteLinksAsync(
        string key,
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        if (!IssueUrls.IsValidKey(key))
        {
            return JiraResults.Failure<IReadOnlyList<RemoteLink>>(
                new JiraError(JiraErrorKind.NotFound, null, "Not found or no access."));
        }

        var result = await transport.SendAsync(
            JiraRequest.Get($"{IssuePath}/{key}/remotelink") with { Priority = priority },
            RemoteLinkJsonContext.Default.ListLink,
            cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(RemoteLinkMapper.Map(result.Value))
            : JiraResults.Failure<IReadOnlyList<RemoteLink>>(result.Error);
    }
}
