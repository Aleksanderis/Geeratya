namespace Geeratya.Jira.Transport;

/// <summary>
/// A failed Jira call. The message never contains credentials and never raw HTML. <see cref="FieldErrors"/> (field id
/// to message, from the v3 <c>errors</c> object) and <see cref="Messages"/> (every <c>errorMessages</c> entry) are
/// filled when the body carried them; they matter for writes.
/// </summary>
public sealed record JiraError(
    JiraErrorKind Kind,
    int? StatusCode,
    string Message,
    TimeSpan? RetryAfter = null,
    IReadOnlyDictionary<string, string>? FieldErrors = null,
    IReadOnlyList<string>? Messages = null);
