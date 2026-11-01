namespace Geeratya.Jira.Tests.Live;

[Trait("Os", "Independent")]
public class LiveEnvironmentTests
{
    [Fact]
    public void GivenNoVariables_WhenCheckingReason_ThenAllThreeAreNamed()
    {
        var reason = LiveEnvironment.MissingReason(_ => null);

        Assert.Contains("GEERATYA_LIVE_HOSTS", reason);
        Assert.Contains("GEERATYA_LIVE_EMAIL", reason);
        Assert.Contains("GEERATYA_LIVE_TOKEN", reason);
    }

    [Fact]
    public void GivenOnlyTokenMissing_WhenCheckingReason_ThenOnlyTokenIsNamed()
    {
        var reason = LiveEnvironment.MissingReason(name => name == LiveEnvironment.TokenVariable ? null : "x");

        Assert.Contains("GEERATYA_LIVE_TOKEN", reason);
        Assert.DoesNotContain("GEERATYA_LIVE_EMAIL", reason);
    }

    [Fact]
    public void GivenAllVariables_WhenCheckingReason_ThenThereIsNone()
    {
        Assert.Null(LiveEnvironment.MissingReason(_ => "x"));
    }

    [Fact]
    public void GivenSeveralHosts_WhenLoading_ThenTheFirstIsTheSite()
    {
        var values = new Dictionary<string, string>
        {
            ["GEERATYA_LIVE_HOSTS"] = "dev.example.net, other.example.net",
            ["GEERATYA_LIVE_EMAIL"] = "a@example.com",
            ["GEERATYA_LIVE_TOKEN"] = "t",
        };

        var environment = LiveEnvironment.Load(name => values[name]);

        Assert.Equal("dev.example.net", environment.Connection.SiteUrl.Host);
    }
}
