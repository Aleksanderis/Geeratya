using System.Net;
using Geeratya.App.Tests.Fakes;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes.Models;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;

namespace Geeratya.App.Tests;

[Trait("Os", "Independent")]
public sealed class WriteHandlerDeleteTests
{
    private const string TwoSubtasks =
        """{"issues":[{"id":"2","key":"A-2","fields":{"summary":"Sub"}},{"id":"3","key":"A-3","fields":{"summary":"Sub"}}],"isLast":true}""";

    private const string NoSubtasks = """{"issues":[],"isLast":true}""";
    private const string MoreSubtasks = """{"issues":[{"id":"2","key":"A-2","fields":{"summary":"Sub"}}],"isLast":false,"nextPageToken":"t"}""";
    private const string Forbidden = """{"errorMessages":["You do not have permission to delete issues in this project."],"errors":{}}""";
    private const string HasSubtasks = """{"errorMessages":["The issue 'A-1' has subtasks."],"errors":{}}""";
    private const string StillThere = """{"id":"1","key":"A-1","fields":{"summary":"One"}}""";

    private static readonly Profile _a = new("a", "https://a.atlassian.net", "me@a.test");

    [Fact]
    public async Task GivenSubtasks_WhenCounting_ThenOneSearchForSubtasksOfThatIssueAnswersTheCount()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, TwoSubtasks));

        await fixture.Handler.StartAsync(new CountSubtasksEffect(7, "A-1"));

        var counted = Assert.IsType<SubtasksCounted>(Assert.Single(fixture.Posted));
        Assert.Equal(7, counted.RequestId);
        Assert.Equal(2, counted.Count);
        Assert.False(counted.More);
        var query = Uri.UnescapeDataString(Assert.Single(fixture.Http.Requests).Query.Replace('+', ' '));
        Assert.Contains("parent = A-1 AND issuetype in subTaskIssueTypes()", query);
        Assert.Equal(["GET"], fixture.Http.Methods);
    }

    [Fact]
    public async Task GivenMoreThanOnePage_WhenCounting_ThenTheCountSaysThereAreMore()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, MoreSubtasks));

        await fixture.Handler.StartAsync(new CountSubtasksEffect(7, "A-1"));

        Assert.True(Assert.IsType<SubtasksCounted>(Assert.Single(fixture.Posted)).More);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task GivenAFailedSearch_WhenCounting_ThenTheCountFailsAndNothingIsDeleted(HttpStatusCode status)
    {
        using var fixture = await ConnectedAsync(h => h.Reply(status, string.Empty));

        await fixture.Handler.StartAsync(new CountSubtasksEffect(7, "A-1"));

        Assert.Equal(7, Assert.IsType<SubtasksCountFailed>(Assert.Single(fixture.Posted)).RequestId);
        Assert.Equal(["GET"], fixture.Http.Methods);
    }

    [Fact]
    public async Task GivenNoConnection_WhenCounting_ThenTheCountFails()
    {
        using var fixture = Disconnected();

        await fixture.Handler.StartAsync(new CountSubtasksEffect(7, "A-1"));

        Assert.Contains("Not connected", Assert.IsType<SubtasksCountFailed>(Assert.Single(fixture.Posted)).Reason);
    }

    [Theory]
    [InlineData(0, "deleteSubtasks=false")]
    [InlineData(2, "deleteSubtasks=true")]
    public async Task GivenConfirmedDelete_WhenExecuting_ThenOneDeleteWithTheFlagAndNoReadAfterwards(
        int subtasks,
        string flag)
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.NoContent, string.Empty));

        await fixture.Handler.StartAsync(
            new ExecuteWriteEffect(41, new DeleteIssueRequest("A-1", "One", subtasks)));

        var succeeded = Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted));
        Assert.Equal(41, succeeded.OperationId);
        Assert.Empty(succeeded.Rows);
        Assert.Equal(["DELETE"], fixture.Http.Methods);
        var request = Assert.Single(fixture.Http.Requests);
        Assert.EndsWith("/rest/api/3/issue/A-1", request.AbsolutePath);
        Assert.Equal($"?{flag}", request.Query);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, Forbidden, WriteErrorKind.Permission)]
    [InlineData(HttpStatusCode.BadRequest, HasSubtasks, WriteErrorKind.Validation)]
    [InlineData(HttpStatusCode.NotFound, "{}", WriteErrorKind.NotFound)]
    public async Task GivenARefusedDelete_WhenExecuting_ThenItFailsDefinitelyWithJirasReason(
        HttpStatusCode status,
        string body,
        WriteErrorKind kind)
    {
        using var fixture = await ConnectedAsync(h => h.Reply(status, body));

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, new DeleteIssueRequest("A-1", "One", 0)));

        var failed = Assert.IsType<WriteFailed>(Assert.Single(fixture.Posted));
        Assert.Equal(kind, failed.Error.Kind);
        Assert.Single(fixture.Http.Requests);
    }

    [Fact]
    public async Task GivenALostDeleteReply_WhenExecuting_ThenTheDeleteIsNotRepeated()
    {
        using var fixture = await ConnectedAsync(
            h => h.Reply(HttpStatusCode.BadGateway, string.Empty).Reply(HttpStatusCode.NoContent, string.Empty),
            3);

        await fixture.Handler.StartAsync(new ExecuteWriteEffect(41, new DeleteIssueRequest("A-1", "One", 0)));

        Assert.IsType<WriteOutcomeUnknown>(Assert.Single(fixture.Posted));
        Assert.Single(fixture.Http.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, VerifyOutcome.Applied)]
    [InlineData(HttpStatusCode.OK, VerifyOutcome.NotApplied)]
    [InlineData(HttpStatusCode.InternalServerError, VerifyOutcome.Unreachable)]
    public async Task GivenALostDeleteReply_WhenVerifying_ThenTheIssueReadDecidesTheOutcome(
        HttpStatusCode status,
        VerifyOutcome outcome)
    {
        var body = status == HttpStatusCode.OK ? StillThere : "{}";
        using var fixture = await ConnectedAsync(h => h.Reply(status, body));

        await fixture.Handler.StartAsync(new VerifyWriteEffect(41, new DeleteIssueRequest("A-1", "One", 0)));

        Assert.Equal(outcome, Assert.IsType<WriteVerified>(Assert.Single(fixture.Posted)).Outcome);
        Assert.EndsWith("/rest/api/3/issue/A-1", Assert.Single(fixture.Http.Requests).AbsolutePath);
    }

    [Fact]
    public async Task GivenReducerAndHandler_WhenDeletingAnIssueWithSubtasks_ThenAskedThenDeletedWithThemThenGone()
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.OK, TwoSubtasks)
            .Reply(HttpStatusCode.NoContent, string.Empty));
        var reducer = new AppReducer(DefaultKeymap.Load());
        var state = OpenTab(reducer);

        var asked = PressAll(reducer, state, "Space", "i", "d");
        await fixture.Handler.StartAsync(Assert.Single(asked.Effects));
        var confirm = reducer.Reduce(asked.State, Assert.Single(fixture.Posted));
        var confirmed = reducer.Reduce(confirm.State, new KeyPressed("y"));
        await fixture.Handler.StartAsync(Assert.IsType<ExecuteWriteEffect>(Assert.Single(confirmed.Effects)));
        var finished = reducer.Reduce(confirmed.State, fixture.Posted[1]);

        Assert.Contains("and its 2 sub-tasks", confirm.State.Confirm!.Message);
        Assert.Equal(["GET", "DELETE"], fixture.Http.Methods);
        Assert.EndsWith("?deleteSubtasks=true", fixture.Http.Requests.Last().Query);
        Assert.Empty(finished.State.Session.Tabs[0].Rows);
        Assert.Equal("Deleted A-1 and its 2 sub-tasks", finished.State.Message);
        Assert.Empty(finished.State.Session.Operations);
    }

    [Fact]
    public async Task GivenReducerAndHandler_WhenDeclining_ThenOnlyTheCountWasSent()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, NoSubtasks));
        var reducer = new AppReducer(DefaultKeymap.Load());

        var asked = PressAll(reducer, OpenTab(reducer), "Space", "i", "d");
        await fixture.Handler.StartAsync(Assert.Single(asked.Effects));
        var confirm = reducer.Reduce(asked.State, Assert.Single(fixture.Posted));
        var declined = reducer.Reduce(confirm.State, new KeyPressed("Esc"));

        Assert.Empty(declined.Effects);
        Assert.Equal(["GET"], fixture.Http.Methods);
        Assert.Single(declined.State.Session.Tabs[0].Rows);
    }

    [Fact]
    public async Task GivenReducerAndHandler_WhenJiraRefusesTheDelete_ThenTheRowStaysWithTheReason()
    {
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.OK, NoSubtasks)
            .Reply(HttpStatusCode.Forbidden, Forbidden));
        var reducer = new AppReducer(DefaultKeymap.Load());

        var asked = PressAll(reducer, OpenTab(reducer), "Space", "i", "d");
        await fixture.Handler.StartAsync(Assert.Single(asked.Effects));
        var confirm = reducer.Reduce(asked.State, Assert.Single(fixture.Posted));
        var confirmed = reducer.Reduce(confirm.State, new KeyPressed("y"));
        await fixture.Handler.StartAsync(Assert.IsType<ExecuteWriteEffect>(Assert.Single(confirmed.Effects)));
        var failed = reducer.Reduce(confirmed.State, fixture.Posted[1]);

        Assert.Single(failed.State.Session.Tabs[0].Rows);
        Assert.Equal(
            "No permission to delete A-1: You do not have permission to delete issues in this project.",
            failed.State.Message);
    }

    private static ReduceResult PressAll(AppReducer reducer, AppState state, params string[] keys)
    {
        var result = new ReduceResult(state, []);

        foreach (var key in keys)
        {
            result = reducer.Reduce(result.State, new KeyPressed(key));
        }

        return result;
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
        var row = new IssueSummary(
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

        return reducer.Reduce(
            opened,
            new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage([row], null, null), DateTimeOffset.UnixEpoch)).State;
    }

    private static Fixture Disconnected()
    {
        var connections = new SiteConnections(
            new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => null)));

        return new Fixture(connections, new ScriptedJiraHandler());
    }

    private static async Task<Fixture> ConnectedAsync(Action<ScriptedJiraHandler> script, int maxRetries = 0)
    {
        var http = new ScriptedJiraHandler();
        var store = new FakeSecretStore();

        script(http);
        await store.SetAsync(_a.CredentialKey, "token-a", CancellationToken.None);

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
