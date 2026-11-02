using Geeratya.Jira.Transport;

namespace Geeratya.Core.State;

/// <summary>
/// The texts of the connection check. Pure. Nothing here receives the token, and the email is never repeated: the
/// profile line already shows it.
/// </summary>
public static class ConnectionText
{
    /// <summary>The state a failed check leaves, with the hint for <paramref name="error"/> on <paramref name="siteUrl"/>.</summary>
    public static ConnectionCheckFailure Failure(JiraError error, string siteUrl)
    {
        var host = HostOf(siteUrl);

        return error.Kind switch
        {
            JiraErrorKind.Unauthorized => new ConnectionCheckFailure(
                ConnectionFailureKind.Unauthorized,
                $"Token or email rejected (401). Replace them: :profile remove, then :profile add."),
            JiraErrorKind.Forbidden => new ConnectionCheckFailure(
                ConnectionFailureKind.Forbidden,
                $"{host} denied access (403): this account may not use the Jira API."),
            JiraErrorKind.Network => new ConnectionCheckFailure(
                ConnectionFailureKind.Network,
                $"Cannot reach {host}. :profile check tries again."),
            JiraErrorKind.Timeout => new ConnectionCheckFailure(
                ConnectionFailureKind.Timeout,
                $"{host} did not answer in time. :profile check tries again."),
            JiraErrorKind.HostRefused => new ConnectionCheckFailure(
                ConnectionFailureKind.HostRefused,
                $"The connection to {host} was refused: {error.Message}"),
            JiraErrorKind.RateLimited => new ConnectionCheckFailure(
                ConnectionFailureKind.RateLimited,
                RateLimited(host, error.RetryAfter)),
            _ => new ConnectionCheckFailure(ConnectionFailureKind.Other, $"{host}: {error.Message}"),
        };
    }

    /// <summary>The state of a connection that could not be built at all (no token and so on).</summary>
    public static ConnectionCheckFailure Setup(string message)
    {
        return new ConnectionCheckFailure(ConnectionFailureKind.Setup, message);
    }

    /// <summary>Replaces control characters of a Jira-sourced name with spaces so they cannot reach the terminal.</summary>
    public static string Sanitize(string text)
    {
        var characters = text.ToCharArray();

        for (var index = 0; index < characters.Length; index++)
        {
            if (char.IsControl(characters[index]))
            {
                characters[index] = ' ';
            }
        }

        return new string(characters);
    }

    /// <summary>The one-line text for the status line and the start screen; empty when nothing was checked.</summary>
    public static string Line(SiteSession session)
    {
        var host = HostOf(session.SiteUrl);

        return session.Connection switch
        {
            Checking => $"Checking {host}...",
            Connected connected => $"Connected as {connected.DisplayName} on {host}",
            ConnectionCheckFailure failure => failure.Hint,
            _ => string.Empty,
        };
    }

    private static string RateLimited(string host, TimeSpan? wait)
    {
        if (wait is not { } span || span <= TimeSpan.Zero)
        {
            return $"{host} is rate limiting requests. :profile check tries again shortly.";
        }

        return $"{host} is rate limiting requests. :profile check tries again in {(int)Math.Ceiling(span.TotalSeconds)} s.";
    }

    private static string HostOf(string? siteUrl)
    {
        return Uri.TryCreate(siteUrl, UriKind.Absolute, out var uri) ? uri.Host : "the site";
    }
}
