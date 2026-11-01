namespace Geeratya.Jira.Tests;

[Trait("Os", "Independent")]
public class JiraConnectionTests
{
    [Fact]
    public void GivenTwoSites_WhenComparingConnections_ThenTheyAreNotEqual()
    {
        var first = new JiraConnection(new Uri("https://one.example.net"), "user1@example.com");
        var second = new JiraConnection(new Uri("https://two.example.net"), "user1@example.com");

        Assert.NotEqual(first, second);
        Assert.Equal("one.example.net", first.SiteUrl.Host);
    }
}
