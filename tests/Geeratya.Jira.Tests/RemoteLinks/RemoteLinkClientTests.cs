using System.Net;
using Geeratya.Jira.RemoteLinks;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Tests.RemoteLinks;

public sealed class RemoteLinkClientTests
{
    private const string GcmPath = "/rest/api/3/issue/GCM-6/remotelink";

    [Fact]
    public async Task GivenConfluenceAndWebLinks_WhenGetting_ThenUrlTitleAndApplicationAreMapped()
    {
        using var fixture = Serving(GcmPath, "links/remotelink-list-gcm6.json");

        var result = await new RemoteLinkClient(fixture.Transport).GetRemoteLinksAsync("GCM-6", CancellationToken.None);

        var links = result.Value;
        Assert.Equal(3, links.Count);
        Assert.Equal("10000", links[0].Id);
        Assert.Equal("Design notes", links[0].Title);
        Assert.Equal("https://example.atlassian.net/wiki/spaces/DEV/pages/1", links[0].Url);
        Assert.Equal("Confluence", links[0].ApplicationName);
        Assert.Null(links[1].ApplicationName);
        Assert.Equal("https://example.com/untitled", links[2].Title);
    }

    [Fact]
    public async Task GivenTeamManagedLink_WhenGetting_ThenLinkIsMapped()
    {
        using var fixture = Serving("/rest/api/3/issue/GTM-5/remotelink", "links/remotelink-list-gtm5.json");

        var result = await new RemoteLinkClient(fixture.Transport).GetRemoteLinksAsync("GTM-5", CancellationToken.None);

        var link = Assert.Single(result.Value);
        Assert.Equal("Remote page", link.Title);
        Assert.Equal("https://example.com/remote", link.Url);
        Assert.Null(link.ApplicationName);
    }

    [Fact]
    public async Task GivenRecordedDevSiteRemoteLink_WhenGetting_ThenLinkIsMapped()
    {
        using var fixture = Serving("/rest/api/3/issue/GCM-10/remotelink", "links/remotelink-list-gcm10.json");

        var result = await new RemoteLinkClient(fixture.Transport).GetRemoteLinksAsync("GCM-10", CancellationToken.None);

        var link = Assert.Single(result.Value);
        Assert.Equal("10001", link.Id);
        Assert.Equal("Seed remote link", link.Title);
        Assert.Equal("https://example.com/geeratya-seed", link.Url);
        Assert.Null(link.ApplicationName);
    }

    [Fact]
    public async Task GivenNonHttpScheme_WhenGetting_ThenUrlIsPassedThroughForTheOpenGate()
    {
        using var fixture = Serving(GcmPath, """[{"id": 1, "object": {"url": "javascript:alert(1)"}}]""", isJson: true);

        var result = await new RemoteLinkClient(fixture.Transport).GetRemoteLinksAsync("GCM-6", CancellationToken.None);

        Assert.Equal("javascript:alert(1)", Assert.Single(result.Value).Url);
    }

    [Fact]
    public async Task GivenNoLinks_WhenGetting_ThenListIsEmpty()
    {
        using var fixture = Serving("/rest/api/3/issue/GCM-2/remotelink", "links/remotelink-list-empty.json");

        var result = await new RemoteLinkClient(fixture.Transport).GetRemoteLinksAsync("GCM-2", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task GivenEntriesWithoutUrl_WhenGetting_ThenTheyAreSkipped()
    {
        const string json = """
            [null, {"id": 1}, {"id": 2, "object": {"title": "x"}},
             {"id": 3, "object": {"url": "https://a.test"}}]
            """;
        using var fixture = Serving(GcmPath, json, isJson: true);

        var result = await new RemoteLinkClient(fixture.Transport).GetRemoteLinksAsync("GCM-6", CancellationToken.None);

        var link = Assert.Single(result.Value);
        Assert.Equal("3", link.Id);
        Assert.Equal("https://a.test", link.Title);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, JiraErrorKind.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, JiraErrorKind.Forbidden)]
    public async Task GivenErrorStatus_WhenGetting_ThenTypedErrorIsReturned(HttpStatusCode status, JiraErrorKind kind)
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, GcmPath, FakeResponse.WithStatus(status));

        var result = await new RemoteLinkClient(fixture.Transport).GetRemoteLinksAsync("GCM-6", CancellationToken.None);

        Assert.Equal(kind, result.Error.Kind);
    }

    [Fact]
    public async Task GivenPersistentRateLimit_WhenGetting_ThenRateLimitedErrorIsReturned()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, GcmPath, FakeResponse.RateLimited("1"));

        var result = await new RemoteLinkClient(fixture.Transport).GetRemoteLinksAsync("GCM-6", CancellationToken.None);

        Assert.Equal(JiraErrorKind.RateLimited, result.Error.Kind);
    }

    [Fact]
    public async Task GivenInvalidKey_WhenGetting_ThenNotFoundWithoutRequest()
    {
        using var fixture = new TransportFixture();

        var result = await new RemoteLinkClient(fixture.Transport).GetRemoteLinksAsync("nope", CancellationToken.None);

        Assert.Equal(JiraErrorKind.NotFound, result.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    private static TransportFixture Serving(string path, string fixtureOrJson, bool isJson = false)
    {
        var body = isJson ? fixtureOrJson : FixtureFiles.Read(fixtureOrJson);
        var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, path, FakeResponse.Ok(body));

        return fixture;
    }
}
