using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Issues;

/// <summary>
/// Reads one issue with the fields the detail view shows. Reads only; comments and remote links are separate
/// clients. A missing or hidden issue is a <see cref="JiraErrorKind.NotFound"/> ("not found or no access").
/// </summary>
public sealed class IssueClient(JiraTransport transport)
{
    private const string IssuePath = "/rest/api/3/issue";

    /// <summary>Loads an issue by key. A string that is not an issue key is answered as NotFound locally.</summary>
    public async Task<JiraResult<IssueDetail>> GetIssueAsync(
        string key,
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        if (!IssueUrls.IsValidKey(key))
        {
            return JiraResults.Failure<IssueDetail>(
                new JiraError(JiraErrorKind.NotFound, null, "Not found or no access."));
        }

        var request = JiraRequest.Get($"{IssuePath}/{key}?fields={IssueFields.DetailQueryValue}")
            with { Priority = priority };

        var result = await transport.SendAsync(request, IssueJsonContext.Default.Issue, cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(IssueDetailMapper.Map(result.Value))
            : JiraResults.Failure<IssueDetail>(result.Error);
    }
}
