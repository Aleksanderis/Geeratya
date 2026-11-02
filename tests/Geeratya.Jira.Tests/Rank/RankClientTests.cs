using System.Net;
using System.Text.Json;
using Geeratya.Jira.Rank;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Tests.Rank;

public sealed class RankClientTests
{
    private const string RankPath = "/rest/agile/1.0/issue/rank";
    private const string BacklogPath = "/rest/agile/1.0/backlog/issue";

    [Fact]
    public async Task GivenNoContent_WhenRankingBefore_ThenEveryIssueIsRankedAndBodyNamesReference()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Put, RankPath, FakeResponse.WithStatus(HttpStatusCode.NoContent));

        var result = await new RankClient(fixture.Transport)
            .RankBeforeAsync(["GCM-3", "GCM-4"], "GCM-6", "customfield_10019", Cancel());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.AllRanked);
        Assert.Equal(["GCM-3", "GCM-4"], result.Value.Items.Select(item => item.Key));
        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal(
            ["GCM-3", "GCM-4"],
            body.RootElement.GetProperty("issues").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal("GCM-6", body.RootElement.GetProperty("rankBeforeIssue").GetString());
        Assert.Equal(10019, body.RootElement.GetProperty("rankCustomFieldId").GetInt64());
        Assert.False(body.RootElement.TryGetProperty("rankAfterIssue", out _));
    }

    [Fact]
    public async Task GivenNoRankField_WhenRankingAfter_ThenBodyHasAfterAndNoCustomFieldId()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Put, RankPath, FakeResponse.WithStatus(HttpStatusCode.NoContent));

        var result = await new RankClient(fixture.Transport).RankAfterAsync(["GTM-5"], "GTM-4", null, Cancel());

        Assert.True(result.Value.AllRanked);
        using var body = JsonDocument.Parse(Assert.Single(fixture.Handler.Requests).Body!);
        Assert.Equal("GTM-4", body.RootElement.GetProperty("rankAfterIssue").GetString());
        Assert.False(body.RootElement.TryGetProperty("rankCustomFieldId", out _));
    }

    [Fact]
    public async Task GivenMultiStatusWithOneFailure_WhenRanking_ThenSuccessWithPerIssueResults()
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read("writes/synthetic-rank-207-partial.json");
        fixture.Handler.Serve(HttpMethod.Put, RankPath, FakeResponse.WithStatus((HttpStatusCode)207, body));

        var result = await new RankClient(fixture.Transport)
            .RankBeforeAsync(["GCM-3", "GCM-4", "GCM-5"], "GCM-6", null, Cancel());

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.AllRanked);
        Assert.True(result.Value.Items[0].IsRanked);
        Assert.False(result.Value.Items[1].IsRanked);
        Assert.Equal(["Could not rank the issue, try again."], result.Value.Items[1].Errors);
        Assert.Equal(["GCM-4", "GCM-5"], result.Value.Failed.Select(item => item.Key));
        Assert.Equal(["Jira did not report this issue."], result.Value.Items[2].Errors);
    }

    [Fact]
    public async Task GivenReferenceIssueGone_WhenRanking_ThenValidationErrorWithJirasMessage()
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read("boards/rank-400-missing-issue.json");
        fixture.Handler.Serve(HttpMethod.Put, RankPath, FakeResponse.WithStatus(HttpStatusCode.BadRequest, body));

        var result = await new RankClient(fixture.Transport).RankBeforeAsync(["GCM-3"], "GCM-99", null, Cancel());

        Assert.Equal(WriteErrorKind.Validation, result.Error.Kind);
        Assert.Contains("does not exist or you do not have permission", result.Error.Messages.Single());
        Assert.False(result.Error.IsOutcomeUnknown);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenNoContent_WhenMovingToBacklog_ThenPostsIssuesOnly()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, BacklogPath, FakeResponse.WithStatus(HttpStatusCode.NoContent));

        var result = await new RankClient(fixture.Transport).MoveToBacklogAsync(["GCM-1", "GCM-2"], Cancel());

        Assert.True(result.Value.AllRanked);
        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("""{"issues":["GCM-1","GCM-2"]}""", request.Body);
    }

    [Fact]
    public async Task GivenMultiStatusFromBacklog_WhenMoving_ThenPerIssueResultsAreMapped()
    {
        using var fixture = new TransportFixture();
        const string body = """
            {"entries":[{"issueKey":"GCM-1","status":200},{"issueKey":"GCM-2","status":400,"errors":["Nope"]}]}
            """;
        fixture.Handler.Serve(HttpMethod.Post, BacklogPath, FakeResponse.WithStatus((HttpStatusCode)207, body));

        var result = await new RankClient(fixture.Transport).MoveToBacklogAsync(["GCM-1", "GCM-2"], Cancel());

        Assert.Equal([true, false], result.Value.Items.Select(item => item.IsRanked));
    }

    [Fact]
    public async Task GivenInvalidInput_WhenRankingOrMoving_ThenValidationAndNoRequest()
    {
        using var fixture = new TransportFixture();
        var client = new RankClient(fixture.Transport);
        var tooMany = Enumerable.Range(1, 51).Select(number => $"GCM-{number}").ToList();

        var results = new[]
        {
            (await client.RankBeforeAsync([], "GCM-6", null, Cancel()), "At least one issue"),
            (await client.RankBeforeAsync(["gcm-3"], "GCM-6", null, Cancel()), "Every issue key"),
            (await client.RankBeforeAsync(["GCM-3", "GCM-3"], "GCM-6", null, Cancel()), "Every issue key"),
            (await client.RankBeforeAsync(["GCM-3"], "bad", null, Cancel()), "reference issue key"),
            (await client.RankBeforeAsync(["GCM-3"], "GCM-3", null, Cancel()), "relative to itself"),
            (await client.RankBeforeAsync(["GCM-3"], "GCM-6", "rank", Cancel()), "rank field id"),
            (await client.RankBeforeAsync(["GCM-3"], "GCM-6", "customfield_+5", Cancel()), "rank field id"),
            (await client.RankBeforeAsync(["GCM-3"], "GCM-6", "customfield_ 5", Cancel()), "rank field id"),
            (await client.RankAfterAsync(tooMany, "GCM-99", null, Cancel()), "At most 50"),
            (await client.MoveToBacklogAsync([], Cancel()), "At least one issue"),
            (await client.MoveToBacklogAsync(["x"], Cancel()), "Every issue key"),
        };

        Assert.All(results, entry =>
        {
            Assert.Equal(WriteErrorKind.Validation, entry.Item1.Error.Kind);
            Assert.Contains(entry.Item2, entry.Item1.Error.Message);
        });
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenReferenceThatWasRankedBefore_WhenRankingTwice_ThenBothBodiesKeepGivenOrderAndReference()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Put, RankPath, FakeResponse.WithStatus(HttpStatusCode.NoContent));
        var client = new RankClient(fixture.Transport);

        await client.RankBeforeAsync(["GCM-3"], "GCM-6", null, Cancel());
        await client.RankAfterAsync(["GCM-6", "GCM-2"], "GCM-4", null, Cancel());

        var bodies = fixture.Handler.Requests.Select(request => request.Body).ToList();
        Assert.Equal("""{"issues":["GCM-3"],"rankBeforeIssue":"GCM-6"}""", bodies[0]);
        Assert.Equal("""{"issues":["GCM-6","GCM-2"],"rankAfterIssue":"GCM-4"}""", bodies[1]);
    }

    [Fact]
    public async Task GivenLostReply_WhenRanking_ThenUnknownOutcomeAndNotRetried()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Put, RankPath, FakeResponse.Broken());

        var result = await new RankClient(fixture.Transport).RankBeforeAsync(["GCM-3"], "GCM-6", null, Cancel());

        Assert.True(result.Error.IsOutcomeUnknown);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenUnreadableMultiStatusBody_WhenRanking_ThenUnknownOutcome()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Put, RankPath, FakeResponse.WithStatus((HttpStatusCode)207, "<html>"));

        var result = await new RankClient(fixture.Transport).RankBeforeAsync(["GCM-3"], "GCM-6", null, Cancel());

        Assert.True(result.Error.IsOutcomeUnknown);
    }

    [Fact]
    public async Task GivenRateLimitWithRetryAfter_WhenRanking_ThenWaitsAndSucceeds()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Script(
            HttpMethod.Put,
            RankPath,
            FakeResponse.RateLimited("2"),
            FakeResponse.WithStatus(HttpStatusCode.NoContent));

        var result = await new RankClient(fixture.Transport).RankBeforeAsync(["GCM-3"], "GCM-6", null, Cancel());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, fixture.Handler.Requests.Count);
    }

    [Fact]
    public async Task GivenPermissionDenied_WhenMovingToBacklog_ThenPermissionError()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, BacklogPath, FakeResponse.WithStatus(HttpStatusCode.Forbidden));

        var result = await new RankClient(fixture.Transport).MoveToBacklogAsync(["GCM-1"], Cancel());

        Assert.Equal(WriteErrorKind.Permission, result.Error.Kind);
    }

    private static CancellationToken Cancel() => CancellationToken.None;
}
