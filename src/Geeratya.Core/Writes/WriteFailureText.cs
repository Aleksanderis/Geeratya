using System.Globalization;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// The status line for a write that Jira definitely did not apply, from one pure table keyed by the error kind
/// (<c>docs/architecture.md</c>, "Write errors as typed results"). Says what was not done and why; the lost-reply
/// kinds never come here, they are verified first.
/// </summary>
public static class WriteFailureText
{
    private const string Unauthorized = "Jira rejected the API token; fix the profile (:profile) and try again";
    private const string NotSent = "Offline: nothing was sent";
    private const string NoReason = "Jira gave no reason";

    public static string Of(WriteError error, WriteRequest request)
    {
        return error.Kind switch
        {
            WriteErrorKind.Validation => $"Could not {request.ActionText}: {Reason(error)}",
            WriteErrorKind.Unauthorized => $"Could not {request.ActionText}: {Unauthorized}",
            WriteErrorKind.Permission => $"No permission to {request.ActionText}: {Reason(error)}",
            WriteErrorKind.NotFound => $"{KeysOf(request)} not found or no access",
            WriteErrorKind.Conflict => $"{KeysOf(request)} changed meanwhile; reload and try again",
            WriteErrorKind.RateLimited => $"Could not {request.ActionText}: {Wait(error)}",
            WriteErrorKind.NotSent => $"Could not {request.ActionText}: {NotSent}",
            _ => $"Could not {request.ActionText}: {Reason(error)}",
        };
    }

    private static string KeysOf(WriteRequest request)
    {
        return request.Keys.Count == 0 ? request.ActionText : string.Join(", ", request.Keys);
    }

    private static string Wait(WriteError error)
    {
        if (error.RetryAfter is not { } wait)
        {
            return "try again in a moment";
        }

        var seconds = (int)Math.Ceiling(wait.TotalSeconds);

        return $"try again in {seconds.ToString(CultureInfo.InvariantCulture)} s";
    }

    private static string Reason(WriteError error)
    {
        if (error.FieldErrors.Count > 0)
        {
            return error.FieldErrors[0].Message;
        }

        if (error.Messages.Count > 0)
        {
            return error.Messages[0];
        }

        return string.IsNullOrWhiteSpace(error.Message) ? NoReason : error.Message;
    }
}
