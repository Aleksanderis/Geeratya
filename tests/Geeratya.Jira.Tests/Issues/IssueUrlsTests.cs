using Geeratya.Jira.Issues;

namespace Geeratya.Jira.Tests.Issues;

[Trait("Os", "Independent")]
public sealed class IssueUrlsTests
{
    private static readonly JiraConnection _connection =
        new(new Uri("https://example.atlassian.net"), "user@example.com");

    [Fact]
    public void GivenValidKey_WhenBuildingBrowseUrl_ThenItPointsAtTheBrowsePage()
    {
        var url = IssueUrls.Browse(_connection, "GCM-12");

        Assert.Equal("https://example.atlassian.net/browse/GCM-12", url?.AbsoluteUri);
    }

    [Fact]
    public void GivenSiteUrlWithPath_WhenBuildingBrowseUrl_ThenOnlySchemeAndHostAreKept()
    {
        var connection = new JiraConnection(new Uri("https://example.atlassian.net/jira/"), "user@example.com");

        var url = IssueUrls.Browse(connection, "A_B2-7");

        Assert.Equal("https://example.atlassian.net/browse/A_B2-7", url?.AbsoluteUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("gcm-1")]
    [InlineData("GCM-1\n")]
    [InlineData("GCM-1/../../admin")]
    [InlineData("GCM-1?x=1")]
    [InlineData("GCM-1#frag")]
    [InlineData("GCM-1 ")]
    [InlineData("@evil.example/GCM-1")]
    [InlineData("GCM-")]
    [InlineData("-1")]
    public void GivenHostileKey_WhenBuildingBrowseUrl_ThenNoUrlIsReturned(string key)
    {
        Assert.Null(IssueUrls.Browse(_connection, key));
    }
}
