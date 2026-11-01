namespace Geeratya.FakeJira;

/// <summary>Settings of one fake Jira run.</summary>
/// <param name="FixturesDirectory">Folder with the recorded fixtures.</param>
/// <param name="Port">Port to listen on; 0 picks a free one.</param>
/// <param name="GeneratedIssues">When above zero, search serves this many generated issues instead of fixtures.</param>
/// <param name="LatencyMilliseconds">Delay added to every reply.</param>
/// <param name="RateLimitEvery">Every Nth request is answered 429 with Retry-After; 0 turns it off.</param>
public sealed record FakeJiraOptions(
    string FixturesDirectory,
    int Port,
    int GeneratedIssues,
    int LatencyMilliseconds,
    int RateLimitEvery);
