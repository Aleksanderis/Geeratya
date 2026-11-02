using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Writes;

/// <summary>Creates <see cref="WriteResult{T}"/> values and maps transport failures to write errors.</summary>
public static class WriteResults
{
    public static WriteResult<T> Success<T>(T value) => new(value, null);

    public static WriteResult<T> Failure<T>(WriteError error) => new(default, error);

    /// <summary>Maps a transport result of a write; the error kind decides what is known about the outcome.</summary>
    public static WriteResult<T> From<T>(JiraResult<T> result) =>
        result.IsSuccess ? Success(result.Value) : Failure<T>(Map(result.Error));

    /// <summary>Maps one transport error. 5xx, network, bad replies and 3xx leave the outcome unknown.</summary>
    public static WriteError Map(JiraError error)
    {
        var kind = error.Kind switch
        {
            JiraErrorKind.BadRequest => WriteErrorKind.Validation,
            JiraErrorKind.Unauthorized => WriteErrorKind.Unauthorized,
            JiraErrorKind.Forbidden => WriteErrorKind.Permission,
            JiraErrorKind.NotFound => WriteErrorKind.NotFound,
            JiraErrorKind.Conflict => WriteErrorKind.Conflict,
            JiraErrorKind.RateLimited => WriteErrorKind.RateLimited,
            JiraErrorKind.Timeout => WriteErrorKind.Timeout,
            JiraErrorKind.HostRefused => WriteErrorKind.NotSent,
            _ => WriteErrorKind.UnknownOutcome,
        };

        var fields = (error.FieldErrors ?? new Dictionary<string, string>())
            .Select(pair => new WriteFieldError(pair.Key, pair.Value))
            .ToList();

        return new WriteError(kind, error.StatusCode, error.Message, fields, error.Messages ?? [], error.RetryAfter);
    }
}
