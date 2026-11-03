using System.Net;
using Geeratya.App.Tests.Fakes;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes.Models;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;

namespace Geeratya.App.Tests;

[Trait("Os", "Independent")]
public sealed class WriteHandlerTests
{
    private const string Assigned =
        """{"issues":[{"id":"1","key":"A-1","fields":{"summary":"One","assignee":{"accountId":"acc-1","displayName":"Eve"}}}],"isLast":true}""";

    private const string StillOther =
        """{"issues":[{"id":"1","key":"A-1","fields":{"summary":"One","assignee":{"accountId":"acc-9","displayName":"Bob"}}}],"isLast":true}""";

    private const string Unassigned =
        """{"issues":[{"id":"1","key":"A-1","fields":{"summary":"One"}}],"isLast":true}""";

    private const string Refused = """{"errorMessages":["User cannot be assigned"],"errors":{}}""";

    private static readonly Profile _a = new("a", "https://a.atlassian.net", "me@a.test");
    private static readonly Profile _b = new("b", "https://b.atlassian.net", "me@b.test");
    private static readonly IssueAssignee _eve = new("acc-1", "Eve");
    private static readonly AssignRequest _assign = new("A-1", _eve);

    [Fact]
    public async Task GivenAcceptedAssign_WhenExecuting_ThenOneWriteThenOneReadAndTheServerRowIsPosted()
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.NoContent, string.Empty)
            .Reply(HttpStatusCode.OK, Assigned));

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, _assign));

        var succeeded = Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted));
        Assert.Equal(41, succeeded.OperationId);
        Assert.Equal("acc-1", Assert.Single(succeeded.Rows).Assignee!.AccountId);
        Assert.Equal(["PUT", "GET"], fixture.Http.Methods);
        Assert.EndsWith("/rest/api/3/issue/A-1/assignee", fixture.Http.Requests.First().AbsolutePath);
        Assert.Contains("acc-1", fixture.Http.Bodies.First());
        Assert.Contains("key in (A-1)", Uri.UnescapeDataString(fixture.Http.Requests.Last().Query.Replace('+', ' ')));
    }

    [Fact]
    public async Task GivenAcceptedAssignButLaggingRead_WhenExecuting_ThenTheStaleRowIsNotPassedOn()
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.NoContent, string.Empty)
            .Reply(HttpStatusCode.OK, StillOther));

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, _assign));

        var succeeded = Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted));
        Assert.Empty(succeeded.Rows);
    }

    [Fact]
    public async Task GivenAcceptedAssignButFailedRead_WhenExecuting_ThenItIsStillASuccessWithNoRows()
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.NoContent, "")
            .Reply(HttpStatusCode.InternalServerError, ""));

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, _assign));

        Assert.Empty(Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted)).Rows);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, WriteErrorKind.Validation)]
    [InlineData(HttpStatusCode.Unauthorized, WriteErrorKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, WriteErrorKind.Permission)]
    [InlineData(HttpStatusCode.NotFound, WriteErrorKind.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests, WriteErrorKind.RateLimited)]
    public async Task GivenARefusedWrite_WhenExecuting_ThenItFailsDefinitelyAndNothingIsRead(
        HttpStatusCode status,
        WriteErrorKind kind)
    {
        using var fixture = await ConnectedAsync(h => h.Reply(status, Refused));

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, _assign));

        var failed = Assert.IsType<WriteFailed>(Assert.Single(fixture.Posted));
        Assert.Equal(kind, failed.Error.Kind);
        Assert.False(failed.Error.IsOutcomeUnknown);
        Assert.Single(fixture.Http.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task GivenALostReply_WhenExecuting_ThenTheOutcomeIsUnknownAndTheWriteIsNotRepeated(
        HttpStatusCode status)
    {
        using var fixture = await ConnectedAsync(h => h.Reply(status, "").Reply(HttpStatusCode.NoContent, ""), 3);

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, _assign));

        var unknown = Assert.IsType<WriteOutcomeUnknown>(Assert.Single(fixture.Posted));
        Assert.True(unknown.Error.IsOutcomeUnknown);
        Assert.Single(fixture.Http.Requests);
    }

    [Fact]
    public async Task GivenNoConnection_WhenExecuting_ThenTheWriteFailsAsNotSent()
    {
        using var fixture = Disconnected();

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, _assign));

        var failed = Assert.IsType<WriteFailed>(Assert.Single(fixture.Posted));
        Assert.Equal(WriteErrorKind.NotSent, failed.Error.Kind);
        Assert.Equal(41, failed.OperationId);
    }

    [Fact]
    public async Task GivenWriteInFlight_WhenTheProfileIsSwitched_ThenTheReplyIsNeverPosted()
    {
        var gate = new TaskCompletionSource();

        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.NoContent, "", gate.Task));

        var run = fixture.Handler.StartAsync(new ExecuteWriteEffect(41, _assign));

        await fixture.Connections.ActivateAsync(_b, 2, CancellationToken.None);
        gate.SetResult();
        await run;

        Assert.Empty(fixture.Posted);
    }

    [Fact]
    public async Task GivenGatedWrite_WhenWaitingForRunning_ThenItWaitsForTheWriteAndReportsSettled()
    {
        var gate = new TaskCompletionSource();
        var never = new TaskCompletionSource();

        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.NoContent, "", gate.Task)
            .Reply(HttpStatusCode.OK, Assigned));

        var handler = new WriteHandler(fixture.Runner, delay: (_, token) => never.Task.WaitAsync(token));
        var run = handler.StartAsync(new ExecuteWriteEffect(41, _assign));
        var waiting = handler.WaitForRunningAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.True(handler.HasRunning);
        Assert.False(waiting.IsCompleted);

        gate.SetResult();
        await run;

        Assert.True(await waiting);
        Assert.False(handler.HasRunning);
    }

    [Fact]
    public async Task GivenGatedWrite_WhenTheTimeoutPasses_ThenWaitingGivesUpWhileTheWriteStaysRunning()
    {
        var gate = new TaskCompletionSource();
        var expire = new TaskCompletionSource();
        var asked = TimeSpan.Zero;

        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.NoContent, "", gate.Task));

        var handler = new WriteHandler(
            fixture.Runner,
            delay: (span, _) =>
            {
                asked = span;

                return expire.Task;
            });

        var run = handler.StartAsync(new ExecuteWriteEffect(41, _assign));
        var waiting = handler.WaitForRunningAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.False(waiting.IsCompleted);

        expire.SetResult();

        Assert.False(await waiting);
        Assert.Equal(TimeSpan.FromSeconds(5), asked);
        Assert.True(handler.HasRunning);

        gate.SetResult();
        await run;
    }

    [Fact]
    public async Task GivenNothingRunning_WhenWaitingForRunning_ThenItReturnsAtOnce()
    {
        using var fixture = await ConnectedAsync(_ => { });

        Assert.True(await fixture.Handler.WaitForRunningAsync(TimeSpan.FromSeconds(5), CancellationToken.None));
    }

    [Fact]
    public async Task GivenWriteInFlight_WhenTheTabScopeIsCancelled_ThenTheWriteStillFinishes()
    {
        var gate = new TaskCompletionSource();

        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.NoContent, "", gate.Task)
            .Reply(HttpStatusCode.OK, Assigned));

        var run = fixture.Handler.StartAsync(new ExecuteWriteEffect(41, _assign));

        // The tab that started the write was closed (any tab id, and the operation id itself).
        fixture.Runner.CancelScope(41);
        fixture.Runner.CancelScope(3);
        gate.SetResult();
        await run;

        Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted));
    }

    [Theory]
    [InlineData(Assigned, VerifyOutcome.Applied)]
    [InlineData(StillOther, VerifyOutcome.Unreachable)]
    [InlineData(Unassigned, VerifyOutcome.Unreachable)]
    public async Task GivenALostReply_WhenVerifying_ThenOnlyARowShowingTheChangeCountsAsApplied(string body, VerifyOutcome expected)
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, body));

        await fixture.Handler.StartAsync(new VerifyWriteEffect(41, _assign));

        var verified = Assert.IsType<WriteVerified>(Assert.Single(fixture.Posted));
        Assert.Equal(expected, verified.Outcome);
        Assert.Equal(41, verified.OperationId);
        Assert.Equal(["GET"], fixture.Http.Methods);
    }

    [Fact]
    public async Task GivenTheCheckCannotRead_WhenVerifying_ThenItIsUnreachable()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.InternalServerError, ""));

        await fixture.Handler.StartAsync(new VerifyWriteEffect(41, _assign));

        Assert.Equal(VerifyOutcome.Unreachable, Assert.IsType<WriteVerified>(Assert.Single(fixture.Posted)).Outcome);
    }

    [Fact]
    public async Task GivenTheIssueIsGone_WhenVerifying_ThenItIsUnreachable()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, """{"issues":[],"isLast":true}"""));

        await fixture.Handler.StartAsync(new VerifyWriteEffect(41, _assign));

        Assert.Equal(VerifyOutcome.Unreachable, Assert.IsType<WriteVerified>(Assert.Single(fixture.Posted)).Outcome);
    }

    [Fact]
    public async Task GivenReducerAndHandler_WhenAssigningToMe_ThenTheRowIsOptimisticThenServerTruth()
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.NoContent, string.Empty)
            .Reply(HttpStatusCode.OK, Assigned));
        var reducer = new AppReducer(DefaultKeymap.Load());
        var state = OpenTab(reducer);
        var tab = state.Session.Tabs[0].Id;

        var started = reducer.Reduce(state, new AssignToMeRequested(tab, "A-1", DateTimeOffset.UnixEpoch));
        var optimistic = started.State.Session.Tabs[0].Rows[0].Assignee;
        var busy = reducer.Reduce(started.State, new AssignToMeRequested(tab, "A-1", DateTimeOffset.UnixEpoch));
        await fixture.Handler.StartAsync(Assert.IsType<ExecuteWriteEffect>(Assert.Single(started.Effects)));
        var finished = reducer.Reduce(started.State, Assert.Single(fixture.Posted));

        Assert.Equal(_eve, optimistic);
        Assert.Equal("A-1 is busy (assigning)", busy.State.Message);
        Assert.Empty(finished.State.Session.Operations);
        Assert.Equal("Assigned A-1 to Eve", finished.State.Message);
        Assert.Equal(_eve, finished.State.Session.Tabs[0].Rows[0].Assignee);
    }

    [Fact]
    public async Task GivenReducerAndHandler_WhenTheWriteIsForbidden_ThenTheRowGoesBackAndTheReasonIsShown()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.Forbidden, Refused));
        var reducer = new AppReducer(DefaultKeymap.Load());
        var state = OpenTab(reducer);

        var tab = state.Session.Tabs[0].Id;

        var started = reducer.Reduce(state, new AssignToMeRequested(tab, "A-1", DateTimeOffset.UnixEpoch));
        await fixture.Handler.StartAsync(Assert.IsType<ExecuteWriteEffect>(Assert.Single(started.Effects)));
        var failed = reducer.Reduce(started.State, Assert.Single(fixture.Posted));

        Assert.Null(failed.State.Session.Tabs[0].Rows[0].Assignee);
        Assert.Empty(failed.State.Session.Operations);
        Assert.Equal("No permission to assign A-1: User cannot be assigned", failed.State.Message);
    }

    [Fact]
    public async Task GivenRankBelow_WhenExecuting_ThenOneRankWriteAndNoReadAreSent()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.NoContent, string.Empty));

        await fixture.Handler.StartAsync(
            new ExecuteWriteEffect(41, new RankRequest("A-1", "A-2", false, "customfield_10019")));

        var succeeded = Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted));
        Assert.Empty(succeeded.Rows);
        Assert.Equal(["PUT"], fixture.Http.Methods);
        Assert.EndsWith("/rest/agile/1.0/issue/rank", Assert.Single(fixture.Http.Requests).AbsolutePath);
        var body = Assert.Single(fixture.Http.Bodies);
        Assert.Contains("\"rankAfterIssue\":\"A-2\"", body);
        Assert.Contains("\"rankCustomFieldId\":10019", body);
        Assert.Contains("\"A-1\"", body);
    }

    [Fact]
    public async Task GivenABlockRank_WhenExecuting_ThenOneRankWriteListsTheIssuesInOrder()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.NoContent, string.Empty));
        var block = new RankBlockRequest(["A-3", "A-1"], "A-2", true, "customfield_10019", ["A-1", "A-2", "A-3"]);

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, block));

        Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted));
        Assert.Equal(["PUT"], fixture.Http.Methods);
        var body = Assert.Single(fixture.Http.Bodies);
        Assert.Contains("\"issues\":[\"A-3\",\"A-1\"]", body);
        Assert.Contains("\"rankBeforeIssue\":\"A-2\"", body);
    }

    [Fact]
    public async Task GivenABlockRankThatJiraPartlyRefuses_WhenExecuting_ThenTheFailureNamesTheIssuesNotRanked()
    {
        const string reply =
            """{"entries":[{"issueKey":"A-1","status":200},{"issueKey":"A-3","status":400,"errors":["Not ranked"]}]}""";
        using var fixture = await ConnectedAsync(h => h.Reply((HttpStatusCode)207, reply));
        var block = new RankBlockRequest(["A-1", "A-3"], "A-2", true, null, ["A-1", "A-2", "A-3"]);

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, block));

        var failed = Assert.IsType<WriteFailed>(Assert.Single(fixture.Posted));
        Assert.Equal(WriteErrorKind.Validation, failed.Error.Kind);
        Assert.Equal("1 of 2 not ranked (A-3): Not ranked", failed.Error.Message);
    }

    [Fact]
    public async Task GivenASprintMove_WhenExecuting_ThenOnePostToTheSprintAndNoReadAreSent()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.NoContent, string.Empty));
        var request = new SprintMoveRequest(["A-1", "A-2"], new IssueSprint(8, "Sprint 2", SprintState.Future), "the backlog");

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, request));

        Assert.EndsWith("/rest/agile/1.0/sprint/8/issue", Assert.Single(fixture.Http.Requests).AbsolutePath);
        Assert.Contains("\"A-1\"", Assert.Single(fixture.Http.Bodies), StringComparison.Ordinal);
        Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted));
    }

    [Fact]
    public async Task GivenAMoveToTheBacklog_WhenExecuting_ThenItPostsToTheBacklogEndpoint()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.NoContent, string.Empty));

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, new SprintMoveRequest(["A-1"], null, "sprint")));

        Assert.EndsWith("/rest/agile/1.0/backlog/issue", Assert.Single(fixture.Http.Requests).AbsolutePath);
    }

    [Fact]
    public async Task GivenASprintMoveThatJiraPartlyRefuses_WhenExecuting_ThenTheFailureSaysHowManyMovedAndWhichNot()
    {
        const string reply =
            """{"entries":[{"issueKey":"A-1","status":200},{"issueKey":"A-3","status":400,"errors":["Epic cannot be moved"]}]}""";
        using var fixture = await ConnectedAsync(h => h.Reply((HttpStatusCode)207, reply));
        var request = new SprintMoveRequest(["A-1", "A-3"], null, "sprint");

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, request));

        var failed = Assert.IsType<WriteFailed>(Assert.Single(fixture.Posted));
        Assert.Equal("1 of 2 moved; A-3 not moved: Epic cannot be moved", failed.Error.Message);
        Assert.Equal(["A-3"], failed.Error.FailedKeys);
    }

    [Fact]
    public async Task GivenASingleIssueJiraRefuses_WhenMoving_ThenTheFailureIsJirasReason()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.BadRequest, Refused));
        var request = new SprintMoveRequest(["A-1"], new IssueSprint(8, "S", SprintState.Future), "sprint");

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, request));

        var failed = Assert.IsType<WriteFailed>(Assert.Single(fixture.Posted));
        Assert.Equal(WriteErrorKind.Validation, failed.Error.Kind);
        Assert.Contains("User cannot be assigned", failed.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenALostReplyOfASprintMove_WhenExecuting_ThenTheOutcomeIsUnknownAndNothingIsRepeated()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.BadGateway, "").Reply(HttpStatusCode.NoContent, ""), 3);

        await fixture.Handler.StartAsync(
            new ExecuteWriteEffect(41, new SprintMoveRequest(["A-1"], null, "sprint")));

        Assert.IsType<WriteOutcomeUnknown>(Assert.Single(fixture.Posted));
        Assert.Single(fixture.Http.Requests);
    }

    [Fact]
    public async Task GivenRankAbove_WhenExecuting_ThenTheBodyNamesTheBeforeIssue()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.NoContent, string.Empty));

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, new RankRequest("A-1", "A-2", true, null)));

        var body = Assert.Single(fixture.Http.Bodies);
        Assert.Contains("\"rankBeforeIssue\":\"A-2\"", body);
        Assert.DoesNotContain("rankCustomFieldId", body);
        Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted));
    }

    [Fact]
    public async Task GivenMultiStatusThatRefusesTheIssue_WhenRanking_ThenItFailsDefinitelyWithJirasText()
    {
        const string body = """{"entries":[{"issueKey":"A-1","status":400,"errors":["Cannot rank an epic here"]}]}""";
        using var fixture = await ConnectedAsync(h => h.Reply((HttpStatusCode)207, body));

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, new RankRequest("A-1", "A-2", true, null)));

        var failed = Assert.IsType<WriteFailed>(Assert.Single(fixture.Posted));
        Assert.Equal(WriteErrorKind.Validation, failed.Error.Kind);
        Assert.Equal("Cannot rank an epic here", failed.Error.Message);
    }

    [Fact]
    public async Task GivenARankIntoAnotherSprint_WhenExecuting_ThenTheMoveGoesFirstAndTheRankSecond()
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.NoContent, string.Empty)
            .Reply(HttpStatusCode.NoContent, string.Empty));

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, CrossRequest()));

        Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted));
        Assert.Equal(["POST", "PUT"], fixture.Http.Methods);
        Assert.EndsWith("/rest/agile/1.0/sprint/8/issue", fixture.Http.Requests.ElementAt(0).AbsolutePath);
        Assert.Contains("\"A-1\"", fixture.Http.Bodies.ElementAt(0));
        Assert.EndsWith("/rest/agile/1.0/issue/rank", fixture.Http.Requests.ElementAt(1).AbsolutePath);
        Assert.Contains("\"rankBeforeIssue\":\"A-2\"", fixture.Http.Bodies.ElementAt(1));
    }

    [Fact]
    public async Task GivenARankIntoAnotherSprintWhoseMoveFails_WhenExecuting_ThenNoRankIsSent()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.Forbidden, string.Empty));

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, CrossRequest()));

        var failed = Assert.IsType<WriteFailed>(Assert.Single(fixture.Posted));
        Assert.Equal(WriteErrorKind.Permission, failed.Error.Kind);
        Assert.Equal(["POST"], fixture.Http.Methods);
    }

    [Fact]
    public async Task GivenARankIntoAnotherSprintWhoseRankFails_WhenExecuting_ThenTheFailureSaysTheIssueMoved()
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.NoContent, string.Empty)
            .Reply(HttpStatusCode.BadRequest, """{"errorMessages":["Gone"],"errors":{}}"""));

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, CrossRequest()));

        var failed = Assert.IsType<WriteFailed>(Assert.Single(fixture.Posted));
        Assert.Equal(WriteErrorKind.Validation, failed.Error.Kind);
        Assert.StartsWith("Moved to sprint \"Sprint 2\" but not ranked", failed.Error.Message, StringComparison.Ordinal);
        Assert.Equal(["POST", "PUT"], fixture.Http.Methods);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, WriteErrorKind.Validation)]
    [InlineData(HttpStatusCode.Forbidden, WriteErrorKind.Permission)]
    [InlineData(HttpStatusCode.TooManyRequests, WriteErrorKind.RateLimited)]
    public async Task GivenARefusedRank_WhenExecuting_ThenItFailsDefinitely(HttpStatusCode status, WriteErrorKind kind)
    {
        using var fixture = await ConnectedAsync(h => h.Reply(status, Refused));

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, new RankRequest("A-1", "A-2", true, null)));

        Assert.Equal(kind, Assert.IsType<WriteFailed>(Assert.Single(fixture.Posted)).Error.Kind);
    }

    [Fact]
    public async Task GivenALostRankReply_WhenVerifying_ThenNothingIsReadAndTheOutcomeIsUnreachable()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.InternalServerError, ""));
        var rank = new RankRequest("A-1", "A-2", true, null);

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, rank));
        await fixture.Handler.StartAsync(new VerifyWriteEffect(41, rank));

        Assert.IsType<WriteOutcomeUnknown>(fixture.Posted[0]);
        Assert.Equal(VerifyOutcome.Unreachable, Assert.IsType<WriteVerified>(fixture.Posted[1]).Outcome);
        Assert.Single(fixture.Http.Requests);
    }

    [Fact]
    public async Task GivenReducerAndHandler_WhenRankingWithTheKeyOnABacklog_ThenTheOrderIsOptimisticThenKept()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.NoContent, string.Empty));
        var reducer = new AppReducer(DefaultKeymap.Load());
        var state = OpenBacklog(reducer);

        var started = reducer.Reduce(state, new KeyPressed("Alt-j", DateTimeOffset.UnixEpoch));
        var optimistic = started.State.Session.Tabs[0].Rows.Select(row => row.Key).ToArray();
        await fixture.Handler.StartAsync(Assert.IsType<ExecuteWriteEffect>(Assert.Single(started.Effects)));
        var finished = reducer.Reduce(started.State, Assert.Single(fixture.Posted));

        Assert.Equal(["A-2", "A-1"], optimistic);
        Assert.Equal(["A-2", "A-1"], finished.State.Session.Tabs[0].Rows.Select(row => row.Key));
        Assert.Equal("Ranked A-1 below A-2", finished.State.Message);
        Assert.Contains(finished.Effects, effect => effect is LoadViewPageEffect);
    }

    [Fact]
    public async Task GivenReducerAndHandler_WhenTheRankIsForbidden_ThenTheOrderGoesBackAndTheReasonIsShown()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.Forbidden, Refused));
        var reducer = new AppReducer(DefaultKeymap.Load());
        var state = OpenBacklog(reducer);

        var started = reducer.Reduce(state, new KeyPressed("Alt-j", DateTimeOffset.UnixEpoch));
        await fixture.Handler.StartAsync(Assert.IsType<ExecuteWriteEffect>(Assert.Single(started.Effects)));
        var failed = reducer.Reduce(started.State, Assert.Single(fixture.Posted));

        Assert.Equal(["A-1", "A-2"], failed.State.Session.Tabs[0].Rows.Select(row => row.Key));
        Assert.Equal("No permission to rank A-1: User cannot be assigned", failed.State.Message);
        Assert.Empty(failed.State.Session.Operations);
    }

    private static AppState OpenBacklog(AppReducer reducer)
    {
        var profiles = ProfileCatalog.Empty.With(_a);
        var start = AppState.Initial with
        {
            Profiles = profiles,
            Session = SiteSession.Empty(_a) with { Connection = new Connected("Eve", "acc-1") },
        };

        var definition = new ViewDefinition(ViewSourceKind.Backlog, "4", ViewLayout.List, "A");
        var opened = reducer.Reduce(start, new ViewOpenRequested(definition)).State;
        var tab = opened.Session.Tabs[0];
        var second = Row() with { Id = "2", Key = "A-2" };
        var page = new IssuePage([Row(), second], null, 2);
        var configuration = new BoardConfiguration(
            4,
            "A",
            BoardType.Kanban,
            [new BoardColumn("To Do", ["1"])],
            "1",
            null,
            "customfield_10019",
            null);
        var configured = reducer.Reduce(opened, new BoardConfigurationLoaded(tab.Id, tab.RequestId, configuration));

        return reducer.Reduce(configured.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, DateTimeOffset.UnixEpoch))
            .State;
    }

    private static AppState OpenTab(AppReducer reducer)
    {
        var profiles = ProfileCatalog.Empty.With(_a);
        var start = AppState.Initial with
        {
            Profiles = profiles,
            Session = SiteSession.Empty(_a) with { Connection = new Connected("Eve", "acc-1") },
        };

        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "A");
        var opened = reducer.Reduce(start, new ViewOpenRequested(definition)).State;
        var tab = opened.Session.Tabs[0];
        var page = new IssuePage([Row()], null, null);

        return reducer.Reduce(opened, new ViewPageLoaded(tab.Id, tab.RequestId, page, DateTimeOffset.UnixEpoch)).State;
    }

    private static IssueSummary Row()
    {
        return new IssueSummary(
            "1",
            "A-1",
            "One",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
    }

    private static Fixture Disconnected()
    {
        var connections = new SiteConnections(
            new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => null)));

        return new Fixture(connections, new ScriptedJiraHandler());
    }

    private static RankMoveRequest CrossRequest()
    {
        var target = new IssueSprint(8, "Sprint 2", SprintState.Future);

        return new RankMoveRequest(["A-1"], "A-2", true, null, ["A-1", "A-2"], target, "the backlog");
    }

    private static async Task<Fixture> ConnectedAsync(Action<ScriptedJiraHandler> script, int maxRetries = 0)
    {
        var http = new ScriptedJiraHandler();
        var store = new FakeSecretStore();

        script(http);
        await store.SetAsync(_a.CredentialKey, "token-a", CancellationToken.None);
        await store.SetAsync(_b.CredentialKey, "token-b", CancellationToken.None);

        var options = new JiraTransportOptions
        {
            MaxRetries = maxRetries,
            Delay = (_, _) => Task.CompletedTask,
            Jitter = () => 0.5,
        };

        var connections = new SiteConnections(
            new TokenProvider(store, new EnvironmentReader(_ => null)),
            new FakeTransportFactory(http, options));

        await connections.ActivateAsync(_a, 2, CancellationToken.None);

        return new Fixture(connections, http);
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture(SiteConnections connections, ScriptedJiraHandler http)
        {
            Connections = connections;
            Http = http;
            Runner = new NetworkRunner(connections, new ListCommandSink(Posted), CancellationToken.None);
            Handler = new WriteHandler(Runner);
        }

        public SiteConnections Connections { get; }

        public ScriptedJiraHandler Http { get; }

        public NetworkRunner Runner { get; }

        public WriteHandler Handler { get; }

        public List<Command> Posted { get; } = [];

        public void Dispose()
        {
            Runner.Dispose();
            Connections.Dispose();
        }
    }
}
