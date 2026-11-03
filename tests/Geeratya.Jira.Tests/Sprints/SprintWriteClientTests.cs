using System.Net;
using System.Text.Json;
using Geeratya.Jira.Sprints;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Tests.Sprints;

public sealed class SprintWriteClientTests
{
    private const string SprintPath = "/rest/agile/1.0/sprint";
    private static readonly DateTimeOffset _start = new(2026, 11, 2, 12, 0, 0, TimeSpan.FromHours(2));
    private static readonly DateTimeOffset _end = _start.AddDays(7);

    [Fact]
    public async Task GivenRecordedCreateReply_WhenCreating_ThenPostsBodyAndMapsFutureSprint()
    {
        using var fixture = new TransportFixture();
        var reply = FixtureFiles.Read("scrum/write-sprint-create.json");
        fixture.Handler.Serve(HttpMethod.Post, SprintPath, FakeResponse.WithStatus(HttpStatusCode.Created, reply));

        var result = await new SprintWriteClient(fixture.Transport)
            .CreateAsync("  s172 tmp 6 ", 6, "survey goal", null, null, Cancel());

        var sprint = result.Value;
        Assert.Equal(12, sprint.Id);
        Assert.Equal(WrittenSprintState.Future, sprint.State);
        Assert.Equal("survey goal", sprint.Goal);
        Assert.Equal(6, sprint.OriginBoardId);
        Assert.Null(sprint.StartDate);
        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("""{"name":"s172 tmp 6","originBoardId":6,"goal":"survey goal"}""", request.Body);
    }

    [Fact]
    public async Task GivenDates_WhenCreating_ThenBodyHasUtcIsoWithMilliseconds()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, SprintPath, FakeResponse.WithStatus(HttpStatusCode.Created, SprintJson));

        await new SprintWriteClient(fixture.Transport).CreateAsync("Next", 5, null, _start, _end, Cancel());

        var body = Assert.Single(fixture.Handler.Requests).Body;
        Assert.Equal(
            """
            {"name":"Next","originBoardId":5,"startDate":"2026-11-02T10:00:00.000Z","endDate":"2026-11-09T10:00:00.000Z"}
            """,
            body);
    }

    [Fact]
    public async Task GivenOnlyName_WhenUpdating_ThenPostsPartialBodyWithNothingElse()
    {
        using var fixture = new TransportFixture();
        var reply = FixtureFiles.Read("scrum/write-sprint-partial-update.json");
        fixture.Handler.Serve(HttpMethod.Post, $"{SprintPath}/12", FakeResponse.Ok(reply));

        var result = await new SprintWriteClient(fixture.Transport)
            .UpdateAsync(12, new SprintEdit("s172 tmp 6 renamed", null, null, null), Cancel());

        Assert.Equal("s172 tmp 6 renamed", result.Value.Name);
        Assert.Null(result.Value.Goal);
        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("""{"name":"s172 tmp 6 renamed"}""", request.Body);
    }

    [Fact]
    public async Task GivenACleared_End_WhenReplacing_ThenPutsTheWholeSprintWithoutTheEnd()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Put, $"{SprintPath}/3", FakeResponse.Ok(SprintJson));
        var current = new Sprint(3, "Next", SprintState.Future, "Keep me", _start, _end, null, 5);

        var result = await new SprintWriteClient(fixture.Transport)
            .ReplaceAsync(current, new SprintEdit(null, null, _start, null, ClearEndDate: true), Cancel());

        Assert.True(result.IsSuccess);
        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal(
            """{"state":"future","name":"Next","goal":"Keep me","startDate":"2026-11-02T10:00:00.000Z"}""",
            request.Body);
    }

    [Fact]
    public async Task GivenBothDatesCleared_WhenReplacing_ThenNeitherDateIsSent()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Put, $"{SprintPath}/3", FakeResponse.Ok(SprintJson));
        var current = new Sprint(3, "Next", SprintState.Future, null, _start, _end, null, 5);

        await new SprintWriteClient(fixture.Transport)
            .ReplaceAsync(current, new SprintEdit(null, null, null, null, true, true), Cancel());

        Assert.Equal("""{"state":"future","name":"Next"}""", Assert.Single(fixture.Handler.Requests).Body);
    }

    [Fact]
    public async Task GivenAnEditThatClearsADate_WhenUpdatingWithThePartialUpdate_ThenItIsRefusedWithoutARequest()
    {
        using var fixture = new TransportFixture();

        var result = await new SprintWriteClient(fixture.Transport)
            .UpdateAsync(3, new SprintEdit(null, null, null, null, ClearEndDate: true), Cancel());

        Assert.False(result.IsSuccess);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenEmptyGoal_WhenUpdating_ThenGoalIsSentAsEmptyString()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, $"{SprintPath}/3", FakeResponse.Ok(SprintJson));

        await new SprintWriteClient(fixture.Transport).UpdateAsync(3, new SprintEdit(null, "", null, null), Cancel());

        Assert.Equal("""{"goal":""}""", Assert.Single(fixture.Handler.Requests).Body);
    }

    [Fact]
    public async Task GivenRecordedStartReply_WhenStarting_ThenPostsStateAndDatesOnlyAndKeepsGoal()
    {
        using var fixture = new TransportFixture();
        var reply = FixtureFiles.Read("scrum/write-sprint-post-start-keeps-goal.json");
        fixture.Handler.Serve(HttpMethod.Post, $"{SprintPath}/16", FakeResponse.Ok(reply));

        var result = await new SprintWriteClient(fixture.Transport).StartAsync(16, _start, _end, Cancel());

        Assert.Equal(WrittenSprintState.Active, result.Value.State);
        Assert.Equal("keep me", result.Value.Goal);
        Assert.Equal(_end.UtcDateTime, result.Value.EndDate?.UtcDateTime);
        Assert.Equal(
            """{"state":"active","startDate":"2026-11-02T10:00:00.000Z","endDate":"2026-11-09T10:00:00.000Z"}""",
            Assert.Single(fixture.Handler.Requests).Body);
    }

    [Fact]
    public async Task GivenRecordedCompleteReply_WhenCompleting_ThenPostsOnlyStateClosedAndMapsCompleteDate()
    {
        using var fixture = new TransportFixture();
        var reply = FixtureFiles.Read("scrum/write-sprint-post-complete-no-dates.json");
        fixture.Handler.Serve(HttpMethod.Post, $"{SprintPath}/16", FakeResponse.Ok(reply));

        var result = await new SprintWriteClient(fixture.Transport).CompleteAsync(16, Cancel());

        Assert.Equal(WrittenSprintState.Closed, result.Value.State);
        Assert.NotNull(result.Value.CompleteDate);
        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal("""{"state":"closed"}""", request.Body);
        Assert.False(JsonDocument.Parse(request.Body!).RootElement.TryGetProperty("moveIssuesTo", out _));
    }

    [Theory]
    [InlineData("scrum/error-sprint-future-to-closed-400.json", SprintWriteProblem.NotActive)]
    [InlineData("scrum/error-sprint-reopen-closed-400.json", SprintWriteProblem.AlreadyClosed)]
    [InlineData("scrum/error-sprint-complete-future-no-start-400.json", SprintWriteProblem.StartDateMissing)]
    [InlineData("scrum/error-sprint-complete-moveissuesto-400.json", SprintWriteProblem.Other)]
    public async Task GivenRecordedBadRequest_WhenCompleting_ThenValidationClassifiedAsProblem(
        string fixtureName,
        SprintWriteProblem expected)
    {
        using var fixture = new TransportFixture();
        var reply = FixtureFiles.Read(fixtureName);
        var bad = FakeResponse.WithStatus(HttpStatusCode.BadRequest, reply);
        fixture.Handler.Serve(HttpMethod.Post, $"{SprintPath}/14", bad);

        var result = await new SprintWriteClient(fixture.Transport).CompleteAsync(14, Cancel());

        Assert.Equal(WriteErrorKind.Validation, result.Error.Kind);
        Assert.Equal(expected, SprintWriteErrors.Classify(result.Error));
        Assert.False(result.Error.IsOutcomeUnknown);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenReopenAttempt_WhenStartingClosedSprint_ThenAlreadyClosed()
    {
        using var fixture = new TransportFixture();
        var reply = FixtureFiles.Read("scrum/error-sprint-reopen-closed-400.json");
        var bad = FakeResponse.WithStatus(HttpStatusCode.BadRequest, reply);
        fixture.Handler.Serve(HttpMethod.Post, $"{SprintPath}/6", bad);

        var result = await new SprintWriteClient(fixture.Transport).StartAsync(6, _start, _end, Cancel());

        Assert.Equal(SprintWriteProblem.AlreadyClosed, SprintWriteErrors.Classify(result.Error));
    }

    [Fact]
    public async Task GivenUnknownSprint_WhenUpdating_ThenNotFound()
    {
        using var fixture = new TransportFixture();
        var reply = FixtureFiles.Read("scrum/error-sprint-404.json");
        var missing = FakeResponse.WithStatus(HttpStatusCode.NotFound, reply);
        fixture.Handler.Serve(HttpMethod.Post, $"{SprintPath}/99", missing);

        var result = await new SprintWriteClient(fixture.Transport)
            .UpdateAsync(99, new SprintEdit("x", null, null, null), Cancel());

        Assert.Equal(WriteErrorKind.NotFound, result.Error.Kind);
        Assert.Equal(SprintWriteProblem.Other, SprintWriteErrors.Classify(result.Error));
    }

    [Fact]
    public async Task GivenInvalidInput_WhenWriting_ThenValidationProblemAndNoRequest()
    {
        using var fixture = new TransportFixture();
        var client = new SprintWriteClient(fixture.Transport);

        var noName = await client.CreateAsync(" ", 6, null, null, null, Cancel());
        var noBoard = await client.CreateAsync("A", 0, null, null, null, Cancel());
        var badDates = await client.CreateAsync("A", 6, null, _end, _start, Cancel());
        var startBackwards = await client.StartAsync(1, _end, _start, Cancel());
        var startSame = await client.StartAsync(1, _start, _start, Cancel());
        var badId = await client.CompleteAsync(0, Cancel());
        var emptyEdit = await client.UpdateAsync(1, new SprintEdit(null, null, null, null), Cancel());
        var blankName = await client.UpdateAsync(1, new SprintEdit(" ", null, null, null), Cancel());

        Assert.Equal(SprintWriteProblem.NameMissing, SprintWriteErrors.Classify(noName.Error));
        Assert.Equal(SprintWriteProblem.Other, SprintWriteErrors.Classify(noBoard.Error));
        Assert.Equal(SprintWriteProblem.EndBeforeStart, SprintWriteErrors.Classify(badDates.Error));
        Assert.Equal(SprintWriteProblem.EndBeforeStart, SprintWriteErrors.Classify(startBackwards.Error));
        Assert.Equal(SprintWriteProblem.EndBeforeStart, SprintWriteErrors.Classify(startSame.Error));
        Assert.Equal(WriteErrorKind.Validation, badId.Error.Kind);
        Assert.Equal(WriteErrorKind.Validation, emptyEdit.Error.Kind);
        Assert.Equal(SprintWriteProblem.NameMissing, SprintWriteErrors.Classify(blankName.Error));
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenOtherStartDateError_WhenStarting_ThenNotReportedAsMissingDate()
    {
        using var fixture = new TransportFixture();
        const string reply = """{"errorMessages":[],"errors":{"startDate":"Invalid date format."}}""";
        fixture.Handler.Serve(
            HttpMethod.Post,
            $"{SprintPath}/5",
            FakeResponse.WithStatus(HttpStatusCode.BadRequest, reply));

        var result = await new SprintWriteClient(fixture.Transport).StartAsync(5, _start, _end, Cancel());

        Assert.Equal(SprintWriteProblem.Other, SprintWriteErrors.Classify(result.Error));
    }

    [Fact]
    public async Task GivenReplyWithoutId_WhenCompleting_ThenUnknownOutcome()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, $"{SprintPath}/4", FakeResponse.Ok("{}"));

        var result = await new SprintWriteClient(fixture.Transport).CompleteAsync(4, Cancel());

        Assert.True(result.Error.IsOutcomeUnknown);
    }

    [Fact]
    public async Task GivenLostReply_WhenCompleting_ThenUnknownOutcomeAndNotRetried()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, $"{SprintPath}/4", FakeResponse.Broken());

        var result = await new SprintWriteClient(fixture.Transport).CompleteAsync(4, Cancel());

        Assert.True(result.Error.IsOutcomeUnknown);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenServerError_WhenStarting_ThenUnknownOutcomeAndNotRetried()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, $"{SprintPath}/4", FakeResponse.WithStatus(HttpStatusCode.BadGateway));

        var result = await new SprintWriteClient(fixture.Transport).StartAsync(4, _start, _end, Cancel());

        Assert.True(result.Error.IsOutcomeUnknown);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenRateLimitWithRetryAfter_WhenCompleting_ThenWaitsAndSucceeds()
    {
        using var fixture = new TransportFixture();
        var reply = FixtureFiles.Read("scrum/write-sprint-complete.json");
        fixture.Handler.Script(
            HttpMethod.Post,
            $"{SprintPath}/14",
            FakeResponse.RateLimited("2"),
            FakeResponse.Ok(reply));

        var result = await new SprintWriteClient(fixture.Transport).CompleteAsync(14, Cancel());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, fixture.Handler.Requests.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, WriteErrorKind.Permission)]
    [InlineData(HttpStatusCode.Unauthorized, WriteErrorKind.Unauthorized)]
    public async Task GivenDeniedReply_WhenCreating_ThenTypedError(HttpStatusCode status, WriteErrorKind expected)
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, SprintPath, FakeResponse.WithStatus(status));

        var result = await new SprintWriteClient(fixture.Transport).CreateAsync("A", 6, null, null, null, Cancel());

        Assert.Equal(expected, result.Error.Kind);
    }

    private static string SprintJson => """{"id":3,"name":"A","state":"future","originBoardId":5}""";

    private static CancellationToken Cancel() => CancellationToken.None;
}
