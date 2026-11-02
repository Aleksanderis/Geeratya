namespace Geeratya.Jira.Transport;

/// <summary>One request: method, path relative to the site, optional JSON body and whether it may be retried.</summary>
public sealed record JiraRequest(HttpMethod Method, string Path, string? JsonBody, bool IsIdempotent)
{
    /// <summary>Who is waiting for the answer. Defaults to <see cref="JiraPriority.User"/>.</summary>
    public JiraPriority Priority { get; init; } = JiraPriority.User;

    /// <summary>
    /// True when a 2xx with an empty body is a success with no value (a write that answers 204 or, on partial
    /// failure, a body). Reads keep treating an empty body as an unreadable reply.
    /// </summary>
    public bool AllowsEmptyBody { get; init; }

    /// <summary>The same request as background work: yields to user requests and is throttled earlier.</summary>
    public JiraRequest AsBackground() => this with { Priority = JiraPriority.Background };

    /// <summary>A read. Retried automatically on 429, 5xx and network failure.</summary>
    public static JiraRequest Get(string path) => new(HttpMethod.Get, path, null, true);

    /// <summary>A POST that only reads (JQL search). Retried like a GET.</summary>
    public static JiraRequest PostSearch(string path, string jsonBody) => new(HttpMethod.Post, path, jsonBody, true);

    /// <summary>A write. Never retried automatically.</summary>
    public static JiraRequest Write(HttpMethod method, string path, string? jsonBody) =>
        new(method, path, jsonBody, false);
}
