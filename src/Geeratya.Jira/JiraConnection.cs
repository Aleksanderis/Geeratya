namespace Geeratya.Jira;

/// <summary>
/// Identifies one Jira site and the account used on it. Every Jira call is made through a transport bound to one
/// connection; there is no global current site. The API token is not part of it (see <c>JiraApiToken</c>).
/// </summary>
public sealed record JiraConnection(Uri SiteUrl, string AccountEmail);
