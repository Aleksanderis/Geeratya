using System.Net;
using System.Text.Json;
using Geeratya.Jira.Sprints;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Tests.Sprints;

public sealed class SprintMoveClientTests
{
    private const string SprintIssuePath = "/rest/agile/1.0/sprint/7/issue";
    private const string BacklogPath = "/rest/agile/1.0/backlog/issue";

    [Fact]
    public async Task GivenNoContent_WhenMovingToSprint_ThenPostsIssuesToSprintAndAllMoved()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, SprintIssuePath, FakeResponse.WithStatus(HttpStatusCode.NoContent));

        var result = await new SprintMoveClient(fixture.Transport).MoveToSprintAsync(7, ["GCS-3", "GCS-4"], Cancel());

        Assert.True(result.Value.AllMoved);
        Assert.Equal(["GCS-3", "GCS-4"], result.Value.Moved);
        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("""{"issues":["GCS-3","GCS-4"]}""", request.Body);
    }

    [Fact]
    public async Task GivenNoContent_WhenMovingToBacklog_ThenPostsIssuesToBacklog()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, BacklogPath, FakeResponse.WithStatus(HttpStatusCode.NoContent));

        var result = await new SprintMoveClient(fixture.Transport).MoveToBacklogAsync(["GTS-3"], Cancel());

        Assert.Equal(["GTS-3"], result.Value.Moved);
        Assert.Equal("""{"issues":["GTS-3"]}""", Assert.Single(fixture.Handler.Requests).Body);
    }

    [Fact]
    public async Task GivenOneHundredAndTwentyKeys_WhenMoving_ThenSentInChunksOfFifty()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, SprintIssuePath, FakeResponse.WithStatus(HttpStatusCode.NoContent));
        var keys = Enumerable.Range(1, 120).Select(number => $"GCS-{number}").ToList();

        var result = await new SprintMoveClient(fixture.Transport).MoveToSprintAsync(7, keys, Cancel());

        Assert.Equal(keys, result.Value.Moved);
        var sizes = fixture.Handler.Requests.Select(request => CountKeys(request.Body!)).ToList();
        Assert.Equal([50, 50, 20], sizes);
    }

    [Fact]
    public async Task GivenMultiStatus_WhenMoving_ThenFailedKeyIsRejectedWithItsMessage()
    {
        using var fixture = new TransportFixture();
        const string body = """
            {"entries":[{"issueKey":"GCS-3","status":200},{"issueKey":"GCS-4","status":400,"errors":["Nope"]}]}
            """;
        fixture.Handler.Serve(HttpMethod.Post, SprintIssuePath, FakeResponse.WithStatus((HttpStatusCode)207, body));

        var result = await new SprintMoveClient(fixture.Transport).MoveToSprintAsync(7, ["GCS-3", "GCS-4"], Cancel());

        Assert.False(result.Value.AllMoved);
        Assert.Equal(["GCS-3"], result.Value.Moved);
        var rejected = Assert.Single(result.Value.Rejected);
        Assert.Equal("GCS-4", rejected.Key);
        Assert.Equal(["Nope"], rejected.Errors);
        Assert.Null(result.Value.Error);
    }

    [Fact]
    public async Task GivenClosedSprint_WhenMoving_ThenRecordedMessageIsReportedAndNothingMoved()
    {
        using var fixture = new TransportFixture();
        var reply = FixtureFiles.Read("scrum/error-sprint-move-into-closed-400.json");
        fixture.Handler.Serve(HttpMethod.Post, SprintIssuePath, FakeResponse.WithStatus(HttpStatusCode.BadRequest, reply));

        var result = await new SprintMoveClient(fixture.Transport).MoveToSprintAsync(7, ["GCS-3"], Cancel());

        Assert.Empty(result.Value.Moved);
        Assert.Equal(WriteErrorKind.Validation, result.Value.Error!.Kind);
        Assert.Contains("not been completed", result.Value.Error.Message);
        Assert.Equal("GCS-3", Assert.Single(result.Value.Rejected).Key);
    }

    [Fact]
    public async Task GivenSecondChunkRefused_WhenMoving_ThenFirstMovedSecondRejectedThirdNotAttempted()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Script(
            HttpMethod.Post,
            SprintIssuePath,
            FakeResponse.WithStatus(HttpStatusCode.NoContent),
            FakeResponse.WithStatus(HttpStatusCode.Forbidden));
        var keys = Enumerable.Range(1, 120).Select(number => $"GCS-{number}").ToList();

        var result = await new SprintMoveClient(fixture.Transport).MoveToSprintAsync(7, keys, Cancel());

        Assert.Equal(keys.Take(50), result.Value.Moved);
        Assert.Equal(keys.Skip(50).Take(50), result.Value.Rejected.Select(item => item.Key));
        Assert.Equal(keys.Skip(100), result.Value.NotAttempted);
        Assert.Equal(WriteErrorKind.Permission, result.Value.Error!.Kind);
        Assert.Equal(2, fixture.Handler.Requests.Count);
    }

    [Fact]
    public async Task GivenLostReply_WhenMoving_ThenChunkIsUnknownAndRestNotAttempted()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, BacklogPath, FakeResponse.Broken());
        var keys = Enumerable.Range(1, 60).Select(number => $"GCS-{number}").ToList();

        var result = await new SprintMoveClient(fixture.Transport).MoveToBacklogAsync(keys, Cancel());

        Assert.Equal(keys.Take(50), result.Value.Unknown);
        Assert.Equal(keys.Skip(50), result.Value.NotAttempted);
        Assert.Empty(result.Value.Rejected);
        Assert.True(result.Value.Error!.IsOutcomeUnknown);
    }

    [Fact]
    public async Task GivenInvalidInput_WhenMoving_ThenValidationAndNoRequest()
    {
        using var fixture = new TransportFixture();
        var client = new SprintMoveClient(fixture.Transport);

        var results = new[]
        {
            await client.MoveToSprintAsync(0, ["GCS-1"], Cancel()),
            await client.MoveToSprintAsync(7, [], Cancel()),
            await client.MoveToSprintAsync(7, ["gcs-1"], Cancel()),
            await client.MoveToBacklogAsync(["GCS-1", "GCS-1"], Cancel()),
        };

        Assert.All(results, result => Assert.Equal(WriteErrorKind.Validation, result.Error.Kind));
        Assert.Empty(fixture.Handler.Requests);
    }

    private static int CountKeys(string body)
    {
        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("issues").GetArrayLength();
    }

    private static CancellationToken Cancel() => CancellationToken.None;
}
