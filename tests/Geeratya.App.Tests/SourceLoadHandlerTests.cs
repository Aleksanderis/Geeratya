using System.Net;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sources;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Transport;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;
using Geeratya.App.Tests.Fakes;

namespace Geeratya.App.Tests;

public sealed class SourceLoadHandlerTests
{
    private static readonly Profile _a = new("a", "https://a.atlassian.net", "me@a.test");
    private static readonly Profile _b = new("b", "https://b.atlassian.net", "me@b.test");

    [Fact]
    public async Task GivenBoardsAndFiltersRequested_WhenLoaded_ThenBothListsArePostedFromTheRecordedReplies()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("board-list"))
                .Reply(HttpStatusCode.OK, Read("filters", "filter-favourite-empty"))
                .Reply(HttpStatusCode.OK, Read("filters", "filter-my")));

        // Sequential: the scripted fake answers in call order, so concurrent loads would swap replies.
        await fixture.Handler.StartAsync(new LoadSourcesEffect(true, false));
        await fixture.Handler.StartAsync(new LoadSourcesEffect(false, true));

        var boards = fixture.Posted.OfType<BoardsLoaded>().Single();
        var filters = fixture.Posted.OfType<FiltersLoaded>().Single();
        Assert.Equal(2, boards.Boards.Count);
        Assert.NotEmpty(filters.Filters);
        Assert.Contains(fixture.Http.Requests, request => request.AbsolutePath.EndsWith("/rest/agile/1.0/board", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GivenSprintsRequested_WhenLoaded_ThenEverySprintOfTheBoardIsPostedWithoutAStateFilter()
    {
        const string reply = """
            {"startAt": 0, "isLast": true, "values": [
              {"id": 7, "state": "closed", "name": "S1"}, {"id": 8, "state": "active", "name": "S2"}]}
            """;
        using var fixture = await ConnectedAsync(http => http.Reply(HttpStatusCode.OK, reply));

        await fixture.Handler.StartSprintsAsync(new LoadSprintsEffect(6));

        var loaded = Assert.IsType<SprintsLoaded>(Assert.Single(fixture.Posted));
        Assert.Equal(6, loaded.BoardId);
        Assert.Equal(["S1", "S2"], loaded.Sprints.Select(sprint => sprint.Name));
        var request = Assert.Single(fixture.Http.Requests);
        Assert.EndsWith("/rest/agile/1.0/board/6/sprint", request.AbsolutePath, StringComparison.Ordinal);
        Assert.DoesNotContain("state=", request.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenSprintsReplyForbidden_WhenLoaded_ThenAFailureForTheBoardIsPosted()
    {
        using var fixture = await ConnectedAsync(
            http => http.Reply(HttpStatusCode.Forbidden, """{"errorMessages":["no"],"errors":{}}"""));

        await fixture.Handler.StartSprintsAsync(new LoadSprintsEffect(6));

        var failed = Assert.IsType<SprintsFailed>(Assert.Single(fixture.Posted));
        Assert.Equal(6, failed.BoardId);
        Assert.Equal(JiraErrorKind.Forbidden, failed.Error.Kind);
    }

    [Fact]
    public async Task GivenAKanbanBoardRemembered_WhenSprintsAreRequested_ThenNoRequestIsMadeAndTheFailureSaysSo()
    {
        using var fixture = await ConnectedAsync(_ => { });
        JiraServices.Create(fixture.Connections)!.BoardKinds.Remember(6, BoardKind.Kanban);

        await fixture.Handler.StartSprintsAsync(new LoadSprintsEffect(6));

        var failed = Assert.IsType<SprintsFailed>(Assert.Single(fixture.Posted));
        Assert.Contains("Kanban", failed.Error.Message, StringComparison.Ordinal);
        Assert.Empty(fixture.Http.Requests);
    }

    [Fact]
    public async Task GivenOnlyFiltersRequested_WhenLoaded_ThenNoBoardRequestIsMade()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, Read("filters", "filter-favourite-empty"))
                .Reply(HttpStatusCode.OK, Read("filters", "filter-my")));

        await fixture.Handler.StartAsync(new LoadSourcesEffect(false, true));

        Assert.Single(fixture.Posted);
        Assert.DoesNotContain(fixture.Http.Requests, request => request.AbsolutePath.Contains("/agile/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GivenBoardsReplyForbidden_WhenLoaded_ThenAFailureForBoardsIsPosted()
    {
        using var fixture = await ConnectedAsync(
            http => http.Reply(HttpStatusCode.Forbidden, """{"errorMessages":["no"],"errors":{}}"""));

        await fixture.Handler.StartAsync(new LoadSourcesEffect(true, false));

        var failed = Assert.IsType<SourceListFailed>(Assert.Single(fixture.Posted));
        Assert.Equal(SourceListKind.Boards, failed.Kind);
        Assert.Equal(JiraErrorKind.Forbidden, failed.Error.Kind);
    }

    [Fact]
    public async Task GivenLoadInFlight_WhenTheProfileIsSwitched_ThenNothingIsPosted()
    {
        var gate = new TaskCompletionSource();

        using var fixture = await ConnectedAsync(
            http => http.Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("board-list"), gate.Task));

        var run = fixture.Handler.StartAsync(new LoadSourcesEffect(true, false));

        await fixture.Connections.ActivateAsync(_b, 2, CancellationToken.None);
        gate.SetResult();
        await run;

        Assert.Empty(fixture.Posted);
    }

    [Fact]
    public async Task GivenNoConnection_WhenLoading_ThenBothListsFailWithANotConnectedMessage()
    {
        var connections = new SiteConnections(
            new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => null)));
        var posted = new List<Command>();

        using var runner = new NetworkRunner(connections, new ListCommandSink(posted), CancellationToken.None);

        await new SourceLoadHandler(runner).StartAsync(new LoadSourcesEffect(true, true));

        Assert.Equal(2, posted.OfType<SourceListFailed>().Count());
        Assert.All(posted.OfType<SourceListFailed>(), failed => Assert.Contains("Not connected", failed.Error.Message));
    }

    private static string Read(string folder, string name)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", folder, $"{name}.json"));
    }

    private static async Task<Fixture> ConnectedAsync(Action<ScriptedJiraHandler> script)
    {
        var http = new ScriptedJiraHandler();
        var store = new FakeSecretStore();

        script(http);
        await store.SetAsync(_a.CredentialKey, "token-a", CancellationToken.None);
        await store.SetAsync(_b.CredentialKey, "token-b", CancellationToken.None);

        var options = new JiraTransportOptions
        {
            MaxRetries = 0,
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
            Handler = new SourceLoadHandler(Runner);
        }

        public SiteConnections Connections { get; }

        public ScriptedJiraHandler Http { get; }

        public NetworkRunner Runner { get; }

        public SourceLoadHandler Handler { get; }

        public List<Command> Posted { get; } = [];

        public void Dispose()
        {
            Runner.Dispose();
            Connections.Dispose();
        }
    }
}
