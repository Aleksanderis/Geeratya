namespace Geeratya.Jira.Tests;

public class JiraConnectionTests
{
    [Fact]
    public void Connections_to_different_sites_are_not_equal()
    {
        var first = new JiraConnection(new Uri("https://one.example.net"));
        var second = new JiraConnection(new Uri("https://two.example.net"));

        Assert.NotEqual(first, second);
        Assert.Equal("one.example.net", first.SiteUrl.Host);
    }
}
