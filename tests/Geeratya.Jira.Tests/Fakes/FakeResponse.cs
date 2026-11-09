using System.Net;

namespace Geeratya.Jira.Tests.Fakes;

/// <summary>
/// One scripted reply. <c>Hang</c> never answers (and ends when cancelled); <c>Unresponsive</c> never answers and
/// ignores cancellation, like a dead pooled connection whose read does not give up; <c>Gate</c> answers once it
/// completes.
/// </summary>
public sealed record FakeResponse(
    HttpStatusCode Status,
    string Body = "",
    IReadOnlyDictionary<string, string>? Headers = null,
    bool Hang = false,
    bool NetworkFailure = false,
    bool Unresponsive = false,
    bool StalledBody = false,
    Task? Gate = null)
{
    public static FakeResponse Ok(string body) => new(HttpStatusCode.OK, body);

    public static FakeResponse WithStatus(HttpStatusCode status, string body = "") => new(status, body);

    public static FakeResponse RateLimited(string retryAfter) =>
        new((HttpStatusCode)429, string.Empty, new Dictionary<string, string> { ["Retry-After"] = retryAfter });

    public static FakeResponse Hanging() => new(HttpStatusCode.OK, Hang: true);

    public static FakeResponse Stuck() => new(HttpStatusCode.OK, Unresponsive: true);

    /// <summary>Headers arrive, the body never does and the read ignores cancellation.</summary>
    public static FakeResponse StuckBody() => new(HttpStatusCode.OK, StalledBody: true);

    public static FakeResponse Broken() => new(HttpStatusCode.OK, NetworkFailure: true);
}
