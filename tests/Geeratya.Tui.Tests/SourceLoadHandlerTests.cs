using System.Net;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Jira.Transport;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;
using Geeratya.Tui.Tests.Fakes;

namespace Geeratya.Tui.Tests;

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

        await fixture.Handler.StartAsync(new LoadSourcesEffect(true, true));

        var boards = fixture.Posted.OfType<BoardsLoaded>().Single();
        var filters = fixture.Posted.OfType<FiltersLoaded>().Single();
        Assert.Equal(2, boards.Boards.Count);
        Assert.NotEmpty(filters.Filters);
        Assert.Contains(fixture.Http.Requests, request => request.AbsolutePath.EndsWith("/rest/agile/1.0/board", StringComparison.Ordinal));
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

        using var runner = new NetworkRunner(connections, posted.Add, CancellationToken.None);

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
            (connection, token) => new JiraTransport(connection, token, http, options));

        await connections.ActivateAsync(_a, 2, CancellationToken.None);

        return new Fixture(connections, http);
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture(SiteConnections connections, ScriptedJiraHandler http)
        {
            Connections = connections;
            Http = http;
            Runner = new NetworkRunner(connections, Posted.Add, CancellationToken.None);
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
