namespace Geeratya.Jira.Tests.Fakes;

/// <summary>A request as the fake Jira saw it.</summary>
public sealed record RecordedRequest(
    HttpMethod Method,
    string PathAndQuery,
    string Host,
    string? Authorization,
    string? Accept,
    string? UserAgent,
    string? Body);
