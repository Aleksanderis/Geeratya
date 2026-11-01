namespace Geeratya.Jira.Transport;

/// <summary>One request: method, path relative to the site, optional JSON body and whether it may be retried.</summary>
public sealed record JiraRequest(HttpMethod Method, string Path, string? JsonBody, bool IsIdempotent)
{
    /// <summary>A read. Retried automatically on 429, 5xx and network failure.</summary>
    public static JiraRequest Get(string path) => new(HttpMethod.Get, path, null, true);

    /// <summary>A POST that only reads (JQL search). Retried like a GET.</summary>
    public static JiraRequest PostSearch(string path, string jsonBody) => new(HttpMethod.Post, path, jsonBody, true);

    /// <summary>A write. Never retried automatically.</summary>
    public static JiraRequest Write(HttpMethod method, string path, string? jsonBody) =>
        new(method, path, jsonBody, false);
}
