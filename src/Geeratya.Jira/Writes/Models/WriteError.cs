namespace Geeratya.Jira.Writes.Models;

/// <summary>
/// A failed Jira write. The message never contains credentials or raw HTML. <see cref="FieldErrors"/> is keyed by
/// field id; <see cref="Messages"/> holds the general <c>errorMessages</c>.
/// </summary>
public sealed record WriteError(
    WriteErrorKind Kind,
    int? StatusCode,
    string Message,
    IReadOnlyList<WriteFieldError> FieldErrors,
    IReadOnlyList<string> Messages,
    TimeSpan? RetryAfter = null)
{
    /// <summary>
    /// True when the write may have been applied (timeout, lost or unreadable reply). Reload the issue before
    /// offering a retry: a repeated create or comment would duplicate.
    /// </summary>
    public bool IsOutcomeUnknown => Kind is WriteErrorKind.Timeout or WriteErrorKind.UnknownOutcome;
}
