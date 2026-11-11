using Geeratya.Jira.Search;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Tests.Fakes;

namespace Geeratya.Jira.Tests.Search;

public sealed class StringPoolTests
{
    private const string Fixture = "search/search-rows-gcm.json";

    [Fact]
    public void GivenEqualStrings_WhenSharing_ThenTheFirstInstanceIsReturned()
    {
        var pool = new StringPool();
        var first = pool.Share(new string('a', 5));

        var second = pool.Share(new string('a', 5));

        Assert.Same(first, second);
        Assert.Equal(1, pool.Count);
    }

    [Fact]
    public void GivenNull_WhenSharing_ThenNullIsReturned() => Assert.Null(new StringPool().Share(null));

    [Fact]
    public void GivenALongValue_WhenSharing_ThenItIsReturnedUnpooled()
    {
        var pool = new StringPool();
        var value = new string('x', StringPool.MaxLength + 1);

        Assert.Same(value, pool.Share(value));
        Assert.Equal(0, pool.Count);
    }

    [Fact]
    public void GivenAFullPool_WhenSharingANewValue_ThenThePoolIsEmptiedAndNeverExceedsItsCapacity()
    {
        var pool = new StringPool(3);

        for (var index = 0; index < 10; index++)
        {
            pool.Share($"value-{index}");
            Assert.True(pool.Count <= pool.Capacity);
        }

        Assert.Equal(1, pool.Count);
    }

    [Fact]
    public void GivenZeroCapacity_WhenCreated_ThenItHoldsOneEntry() => Assert.Equal(1, new StringPool(0).Capacity);

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
