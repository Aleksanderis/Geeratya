using System.Collections.Specialized;
using System.Net;
using System.Web;
using Geeratya.Jira.Filters;
using Geeratya.Jira.Filters.Models;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Tests.Filters;

public sealed class FilterClientTests
{
    private const string SearchPath = "/rest/api/3/filter/search";

    [Fact]
    public async Task GivenSearchFixture_WhenSearching_ThenFiltersAreMappedWithJqlAndOwner()
    {
        using var fixture = Serving(SearchPath, "filters/filter-search.json");

        var result = await new FilterClient(fixture.Transport)
            .SearchAsync(new FilterSearchQuery(), 0, CancellationToken.None);

        var page = result.Value;
        Assert.True(page.IsLast);
        Assert.Equal(2, page.Total);
        Assert.Equal(["10001", "10003"], page.Items.Select(f => f.Id));
        Assert.Equal("Filter for GCM board", page.Items[0].Name);
        Assert.Equal("project = \"GCM\" ORDER BY Rank ASC", page.Items[0].Jql);
        Assert.Equal("Test User 1", page.Items[0].OwnerName);
        var query = QueryOf(Assert.Single(fixture.Handler.Requests));
        Assert.Equal("jql,owner", query["expand"]);
        Assert.Equal("0", query["startAt"]);
    }

    [Fact]
    public async Task GivenNameAndOversizedPage_WhenSearching_ThenNameIsEscapedAndSizeClamped()
    {
        using var fixture = Serving(SearchPath, "filters/filter-search.json");

        await new FilterClient(fixture.Transport)
            .SearchAsync(new FilterSearchQuery("a&b c", 5000), 7, CancellationToken.None);

        var query = QueryOf(Assert.Single(fixture.Handler.Requests));
        Assert.Equal("a&b c", query["filterName"]);
        Assert.Equal("100", query["maxResults"]);
        Assert.Equal("7", query["startAt"]);
    }

    [Fact]
    public async Task GivenNotLastPage_WhenSearching_ThenNextStartAtFollowsTheItems()
    {
        const string json = """{"startAt": 0, "maxResults": 1, "total": 3, "isLast": false, "values": [{"id": "1", "name": "A"}]}""";
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, SearchPath, FakeResponse.Ok(json));

        var result = await new FilterClient(fixture.Transport)
            .SearchAsync(new FilterSearchQuery(PageSize: 1), 0, CancellationToken.None);

        Assert.False(result.Value.IsLast);
        Assert.Equal(1, result.Value.NextStartAt);
    }

    [Fact]
    public async Task GivenMyFixture_WhenLoading_ThenFavouriteAndLastUsedAreMapped()
    {
        using var fixture = Serving("/rest/api/3/filter/my", "filters/filter-my.json");

        var result = await new FilterClient(fixture.Transport).GetMyAsync(CancellationToken.None);

        Assert.Equal(2, result.Value.Count);
        var filter = result.Value[0];
        Assert.Equal("10003", filter.Id);
        Assert.False(filter.IsFavourite);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 13, 51, 54, 500, TimeSpan.Zero), filter.ApproximateLastUsed);
    }

    [Fact]
    public async Task GivenEmptyFavourites_WhenLoading_ThenListIsEmpty()
    {
        using var fixture = Serving("/rest/api/3/filter/favourite", "filters/filter-favourite-empty.json");

        var result = await new FilterClient(fixture.Transport).GetFavouritesAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task GivenFilterFixture_WhenGetting_ThenFilterIsMapped()
    {
        using var fixture = Serving("/rest/api/3/filter/10001", "filters/filter-get.json");

        var result = await new FilterClient(fixture.Transport).GetAsync("10001", CancellationToken.None);

        Assert.Equal("Filter for GCM board", result.Value.Name);
        Assert.Equal("Test User 1", result.Value.OwnerName);
    }

    [Fact]
    public async Task GivenMissingFilter_WhenGetting_ThenNotFoundSaysNoAccess()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            "/rest/api/3/filter/999",
            FakeResponse.WithStatus(HttpStatusCode.NotFound, FixtureFiles.Read("filters/filter-get-missing.json")));

        var result = await new FilterClient(fixture.Transport).GetAsync("999", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(JiraErrorKind.NotFound, result.Error.Kind);
        Assert.Equal("Not found or no access.", result.Error.Message);
    }

    [Fact]
    public async Task GivenOddFilterJson_WhenGetting_ThenJqlOwnerAndTimeAreNull()
    {
        const string json = """{"id": "5", "name": "X", "owner": {}, "approximateLastUsed": "never", "extra": [1]}""";
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, "/rest/api/3/filter/5", FakeResponse.Ok(json));

        var result = await new FilterClient(fixture.Transport).GetAsync("5", CancellationToken.None);

        var filter = result.Value;
        Assert.Null(filter.Jql);
        Assert.Null(filter.OwnerName);
        Assert.Null(filter.ApproximateLastUsed);
        Assert.False(filter.IsFavourite);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, JiraErrorKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, JiraErrorKind.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, JiraErrorKind.NotFound)]
    public async Task GivenErrorReply_WhenSearching_ThenTypedErrorIsReturned(HttpStatusCode status, JiraErrorKind kind)
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, SearchPath, FakeResponse.WithStatus(status));

        var result = await new FilterClient(fixture.Transport)
            .SearchAsync(new FilterSearchQuery(), 0, CancellationToken.None);

        Assert.Equal(kind, result.Error.Kind);
    }

    [Fact]
    public async Task GivenRateLimitThenOk_WhenListingMine_ThenRequestIsRetriedAfterTheWait()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Script(
            HttpMethod.Get,
            "/rest/api/3/filter/my",
            FakeResponse.RateLimited("2"),
            FakeResponse.Ok(FixtureFiles.Read("filters/filter-my.json")));

        var result = await new FilterClient(fixture.Transport).GetMyAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, fixture.Handler.Requests.Count);
        Assert.Contains(TimeSpan.FromSeconds(2), fixture.Waits);
    }

    private static NameValueCollection QueryOf(RecordedRequest request) =>
        HttpUtility.ParseQueryString(new Uri($"https://x{request.PathAndQuery}").Query);

    private static TransportFixture Serving(string path, string fixtureName)
    {
        var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, path, FakeResponse.Ok(FixtureFiles.Read(fixtureName)));

        return fixture;
    }
}
