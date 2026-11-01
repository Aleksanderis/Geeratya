using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Users;

/// <summary>Reads the current account. Also the credential check when a profile is added or switched.</summary>
public sealed class JiraUserClient(JiraTransport transport)
{
    public const string MyselfPath = "/rest/api/3/myself";

    public Task<JiraResult<JiraUser>> GetMyselfAsync(CancellationToken cancellationToken) =>
        transport.SendAsync(JiraRequest.Get(MyselfPath), JiraJsonContext.Default.JiraUser, cancellationToken);
}
