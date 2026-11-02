using System.Net;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Tests.Comments;

public sealed class CommentClientTests
{
    private const string GcmPath = "/rest/api/3/issue/GCM-6/comment";

    [Fact]
    public async Task GivenCompanyManagedComments_WhenGetting_ThenAuthorsBodiesAndDatesAreMapped()
    {
        using var fixture = Serving(GcmPath, "comments/comments-gcm6.json");

        var result = await new CommentClient(fixture.Transport).GetCommentsAsync("GCM-6", 0, CancellationToken.None);

        var page = result.Value;
        Assert.Equal(3, page.Items.Count);
        Assert.True(page.IsLast);
        Assert.Null(page.NextStartAt);
        Assert.Equal(3, page.Total);
        var first = page.Items[0];
        Assert.Equal("10010", first.Id);
        Assert.Equal(first.Created, first.Updated);
        Assert.Equal("Test User 1", first.Author?.DisplayName);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 18, 0, 0, TimeSpan.FromHours(2)), first.Created);
        var paragraph = Assert.IsType<AdfParagraph>(Assert.Single(first.Body.Content));
        var mention = Assert.IsType<AdfMention>(Assert.Single(paragraph.Children, node => node is AdfMention));
        Assert.Equal("fake-account-2", mention.Id);
        Assert.Equal("@Test User 2", mention.Text);
        Assert.Contains(paragraph.Children, node => node is AdfText { Text: var text } && text.Contains("żółć 日本語 🚀"));
    }

    [Fact]
    public async Task GivenRecordedDevSiteComment_WhenGetting_ThenRichBodyAndMentionAreMapped()
    {
        using var fixture = Serving("/rest/api/3/issue/GCM-10/comment", "comments/comments-gcm10.json");

        var result = await new CommentClient(fixture.Transport).GetCommentsAsync("GCM-10", 0, CancellationToken.None);

        var comment = Assert.Single(result.Value.Items);
        Assert.True(result.Value.IsLast);
        Assert.NotNull(comment.Created);
        Assert.StartsWith("Test User", comment.Author?.DisplayName, StringComparison.Ordinal);
        Assert.IsType<AdfHeading>(comment.Body.Content[0]);
        Assert.Contains(comment.Body.Content, node => node is AdfTable);
    }

    [Fact]
    public async Task GivenInactiveAuthorAndEmptyBody_WhenGetting_ThenNameIsKeptAndBodyIsEmpty()
    {
        using var fixture = Serving(GcmPath, "comments/comments-gcm6.json");

        var result = await new CommentClient(fixture.Transport).GetCommentsAsync("GCM-6", 0, CancellationToken.None);

        Assert.Equal("Former user", result.Value.Items[1].Author?.DisplayName);
        Assert.Empty(result.Value.Items[2].Body.Content);
    }

    [Fact]
    public async Task GivenTeamManagedComments_WhenGetting_ThenRichBodiesAreMapped()
    {
        using var fixture = Serving("/rest/api/3/issue/GTM-5/comment", "adf/comments-gtm5.json");

        var result = await new CommentClient(fixture.Transport).GetCommentsAsync("GTM-5", 0, CancellationToken.None);

        Assert.Equal(2, result.Value.Items.Count);
        Assert.IsType<AdfHeading>(result.Value.Items[0].Body.Content[0]);
        Assert.All(result.Value.Items, comment => Assert.NotNull(comment.Created));
    }

    [Fact]
    public async Task GivenNoComments_WhenGetting_ThenPageIsEmptyAndLast()
    {
        using var fixture = Serving("/rest/api/3/issue/GCM-2/comment", "comments/comments-empty.json");

        var result = await new CommentClient(fixture.Transport).GetCommentsAsync("GCM-2", 0, CancellationToken.None);

        Assert.Empty(result.Value.Items);
        Assert.True(result.Value.IsLast);
        Assert.Equal(0, result.Value.Total);
    }

    [Fact]
    public async Task GivenTwoPages_WhenFollowingNextStartAt_ThenWalkEndsAtTotal()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Script(
            HttpMethod.Get,
            GcmPath,
            FakeResponse.Ok(FixtureFiles.Read("comments/comments-gcm6-page1.json")),
            FakeResponse.Ok(FixtureFiles.Read("comments/comments-gcm6-page2-last.json")));
        var client = new CommentClient(fixture.Transport);
        var ids = new List<string>();
        int? next = 0;

        while (next is { } startAt)
        {
            var result = await client.GetCommentsAsync("GCM-6", startAt, CancellationToken.None, 2);
            ids.AddRange(result.Value.Items.Select(comment => comment.Id));
            next = result.Value.NextStartAt;
        }

        Assert.Equal(["10010", "10011", "10012"], ids);
        Assert.Equal(
            [
                $"{GcmPath}?orderBy=created&startAt=0&maxResults=2",
                $"{GcmPath}?orderBy=created&startAt=2&maxResults=2",
            ],
            fixture.Handler.Requests.Select(request => request.PathAndQuery));
    }

    [Fact]
    public async Task GivenEmptyPageWhileTotalIsLarger_WhenGetting_ThenPageIsLast()
    {
        const string json = """{"startAt": 5, "maxResults": 100, "total": 50, "comments": []}""";
        using var fixture = Serving(GcmPath, json, isJson: true);

        var result = await new CommentClient(fixture.Transport).GetCommentsAsync("GCM-6", 5, CancellationToken.None);

        Assert.True(result.Value.IsLast);
    }

    [Fact]
    public async Task GivenOddEntries_WhenGetting_ThenNullEntryKeepsItsSlotAndMissingFieldsAreNeutral()
    {
        const string json = """
            {"total": 2, "comments": [null, {"id": "7", "body": "not an object", "created": "garbage"}]}
            """;
        using var fixture = Serving(GcmPath, json, isJson: true);

        var result = await new CommentClient(fixture.Transport).GetCommentsAsync("GCM-6", 0, CancellationToken.None);

        Assert.Equal(2, result.Value.Items.Count);
        Assert.True(result.Value.IsLast);
        var comment = result.Value.Items[1];
        Assert.Equal("7", comment.Id);
        Assert.Null(comment.Author);
        Assert.Null(comment.Created);
        Assert.Empty(comment.Body.Content);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, JiraErrorKind.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, JiraErrorKind.Forbidden)]
    [InlineData(HttpStatusCode.Unauthorized, JiraErrorKind.Unauthorized)]
    public async Task GivenErrorStatus_WhenGetting_ThenTypedErrorIsReturned(HttpStatusCode status, JiraErrorKind kind)
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, GcmPath, FakeResponse.WithStatus(status));

        var result = await new CommentClient(fixture.Transport).GetCommentsAsync("GCM-6", 0, CancellationToken.None);

        Assert.Equal(kind, result.Error.Kind);
    }

    [Fact]
    public async Task GivenRateLimitThenSuccess_WhenGetting_ThenRetryAfterIsHonoured()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Script(
            HttpMethod.Get,
            GcmPath,
            FakeResponse.RateLimited("2"),
            FakeResponse.Ok(FixtureFiles.Read("comments/comments-gcm6.json")));

        var result = await new CommentClient(fixture.Transport).GetCommentsAsync("GCM-6", 0, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, fixture.Handler.Requests.Count);
        Assert.Contains(fixture.Waits, wait => wait >= TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task GivenPersistentRateLimit_WhenGetting_ThenRateLimitedErrorIsReturned()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, GcmPath, FakeResponse.RateLimited("1"));

        var result = await new CommentClient(fixture.Transport).GetCommentsAsync("GCM-6", 0, CancellationToken.None);

        Assert.Equal(JiraErrorKind.RateLimited, result.Error.Kind);
    }

    [Theory]
    [InlineData("gcm-6")]
    [InlineData("GCM-6/../x")]
    [InlineData("")]
    public async Task GivenInvalidKey_WhenGetting_ThenNotFoundWithoutRequest(string key)
    {
        using var fixture = new TransportFixture();

        var result = await new CommentClient(fixture.Transport).GetCommentsAsync(key, 0, CancellationToken.None);

        Assert.Equal(JiraErrorKind.NotFound, result.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenOversizedPageAndNegativeStart_WhenGetting_ThenRequestIsClamped()
    {
        using var fixture = Serving(GcmPath, "comments/comments-gcm6.json");

        await new CommentClient(fixture.Transport).GetCommentsAsync("GCM-6", -4, CancellationToken.None, 5000);

        Assert.Equal(
            $"{GcmPath}?orderBy=created&startAt=0&maxResults=100",
            Assert.Single(fixture.Handler.Requests).PathAndQuery);
    }

    private static TransportFixture Serving(string path, string fixtureOrJson, bool isJson = false)
    {
        var body = isJson ? fixtureOrJson : FixtureFiles.Read(fixtureOrJson);
        var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, path, FakeResponse.Ok(body));

        return fixture;
    }
}
