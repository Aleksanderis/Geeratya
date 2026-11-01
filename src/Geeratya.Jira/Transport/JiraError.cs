namespace Geeratya.Jira.Transport;

/// <summary>A failed Jira call. The message never contains credentials and never raw HTML.</summary>
public sealed record JiraError(JiraErrorKind Kind, int? StatusCode, string Message, TimeSpan? RetryAfter = null);
