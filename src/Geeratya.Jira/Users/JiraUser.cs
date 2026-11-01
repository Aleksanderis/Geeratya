namespace Geeratya.Jira.Users;

/// <summary>A Jira account from <c>/rest/api/3/myself</c>. The email can be hidden by privacy settings.</summary>
public sealed record JiraUser(
    string AccountId,
    string DisplayName,
    string? EmailAddress,
    bool Active,
    string? TimeZone,
    string? Locale);
