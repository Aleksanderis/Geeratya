namespace Geeratya.FakeJira;

/// <summary>Settings of one fake Jira run.</summary>
/// <param name="FixturesDirectory">Folder with the recorded fixtures.</param>
/// <param name="Port">Port to listen on; 0 picks a free one.</param>
/// <param name="GeneratedIssues">When above zero, search serves this many generated issues instead of fixtures.</param>
/// <param name="LatencyMilliseconds">Delay added to every reply.</param>
/// <param name="RateLimitEvery">Every Nth request is answered 429 with Retry-After; 0 turns it off.</param>
/// <param name="NoActiveSprint">With generated issues: the generated Scrum board has no active sprint (future and closed only).</param>
/// <param name="Demo">Serve the invented Wayfinder dataset (<see cref="DemoDataset"/>) as a Scrum project instead.</param>
/// <param name="DropWriteEvery">
/// Every Nth write (POST, PUT, DELETE) is applied, then the connection is closed without a reply, so the client
/// cannot tell whether it worked; 0 turns it off.
/// </param>
public sealed record FakeJiraOptions(
    string FixturesDirectory,
    int Port,
    int GeneratedIssues,
    int LatencyMilliseconds,
    int RateLimitEvery,
    int DropWriteEvery = 0,
    bool NoActiveSprint = false,
    bool Demo = false);
