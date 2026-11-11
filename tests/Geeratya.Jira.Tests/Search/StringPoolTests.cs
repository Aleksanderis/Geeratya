using Geeratya.Jira.Search;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Tests.Fakes;

namespace Geeratya.Jira.Tests.Search;

public sealed class StringPoolTests
{
    private const string Fixture = "search/search-rows-gcm.json";

    [Fact]
    public async Task GivenTwoSearchPagesOfOneConnection_WhenMapped_ThenNamesAreSharedAcrossThePages()
    {
        using var fixture = Serving();
        var client = new JiraSearchClient(fixture.Transport);

        var first = await SearchAsync(client);
        var second = await SearchAsync(client);

        var one = first.Items[0];
        var two = second.Items[0];
        Assert.Equal(one.Status.Name, two.Status.Name);
        Assert.Same(one.Status.Name, two.Status.Name);
        Assert.Same(one.ProjectKey, two.ProjectKey);
        Assert.Same(one.IssueType.Name, two.IssueType.Name);
    }

    [Fact]
    public async Task GivenTwoConnections_WhenSearching_ThenTheirPoolsAreNotShared()
    {
        using var left = Serving();
        using var right = Serving();

        var a = await SearchAsync(new JiraSearchClient(left.Transport));
        var b = await SearchAsync(new JiraSearchClient(right.Transport));

        Assert.Equal(a.Items[0].ProjectKey, b.Items[0].ProjectKey);
        Assert.NotSame(a.Items[0].ProjectKey, b.Items[0].ProjectKey);
    }

    private static async Task<IssuePage> SearchAsync(JiraSearchClient client)
    {
        var result = await client.SearchAsync(new IssueSearchQuery("project = GCM"), null, CancellationToken.None);

        Assert.True(result.IsSuccess);

        return result.Value;
    }

    private static TransportFixture Serving()
    {
        var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            JiraSearchClient.SearchPath,
            FakeResponse.Ok(FixtureFiles.Read(Fixture)));

        return fixture;
    }
}
