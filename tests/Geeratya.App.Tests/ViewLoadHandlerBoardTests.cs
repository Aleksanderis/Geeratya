using System.Net;
using Geeratya.Core.Boards;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Transport;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;
using Geeratya.App.Tests.Fakes;

namespace Geeratya.App.Tests;

public sealed class ViewLoadHandlerBoardTests
{
    private const string NotFound = """{"errorMessages":["Board does not exist"],"errors":{}}""";

    private static readonly Profile _a = new("a", "https://a.atlassian.net", "me@a.test");
    private static readonly Profile _b = new("b", "https://b.atlassian.net", "me@b.test");

    [Fact]
    public async Task GivenTeamManagedBoard_WhenOpened_ThenConfigurationAndAllIssuesLoadAndColumnsKeepBoardOrder()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("board-gtm-config"))
                .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("rows-gtm-issues")));

        var state = await OpenAsync(fixture, ViewSourceKind.Board, "3");

        var tab = state.Session.Tabs[0];
        Assert.NotNull(tab.Board);
        Assert.Equal(16, tab.Rows.Count);
        Assert.Equal(LoadState.Idle, tab.Load);
        Assert.Contains("/rest/agile/1.0/board/3/configuration", fixture.Path(0));
        Assert.Contains("/rest/agile/1.0/board/3/issue", fixture.Path(1));

        var groups = GroupOf(tab);
        Assert.Equal(["To Do", "In Progress", "Backlog", "Done"], groups.Columns.Select(column => column.Name));
        Assert.Equal(tab.Rows.Count, groups.Columns.Sum(column => column.Rows.Count));
        Assert.Empty(groups.Unmapped);
        Assert.All(groups.Columns[2].Rows, row => Assert.Equal("10003", row.Status.Id));
    }

    [Fact]
    public async Task GivenOpenBoard_WhenRefreshedByHandAndByTheClock_ThenOnlyTheManualRefreshFetchesTheConfiguration()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("board-gtm-config"))
                .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("rows-gtm-issues"))
                .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("rows-gtm-issues"))
                .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("board-gtm-config"))
                .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("rows-gtm-issues")));
        var opened = await OpenAsync(fixture, ViewSourceKind.Board, "3");
        var loadedAt = opened.Session.Tabs[0].LoadedAt!.Value;

        var byClock = await RunAsync(fixture, opened, new Tick(loadedAt + RefreshSchedule.Interval));
        var byHand = await RunAsync(fixture, byClock, new RefreshRequested(byClock.Session.Tabs[0].Id));

        Assert.Equal(5, fixture.Http.Requests.Count);
        Assert.Contains("/board/3/issue", fixture.Path(2));
        Assert.Contains("/board/3/configuration", fixture.Path(3));
        Assert.Contains("/board/3/issue", fixture.Path(4));
        Assert.Equal(16, byHand.Session.Tabs[0].Rows.Count);
        Assert.Equal(LoadState.Idle, byHand.Session.Tabs[0].Load);
    }

    [Fact]
    public async Task GivenCompanyManagedBoard_WhenOpened_ThenBacklogIsTheFirstColumnAndEveryIssueIsPlaced()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("board-gcm-config"))
                .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("rows-gcm-issues")));

        var state = await OpenAsync(fixture, ViewSourceKind.Board, "4");

        var tab = state.Session.Tabs[0];
        Assert.Equal(24, tab.Rows.Count);

        var groups = GroupOf(tab);
        Assert.Equal(["Backlog", "To Do", "In Progress", "Done"], groups.Columns.Select(column => column.Name));
        Assert.Equal(tab.Rows.Count, groups.Columns.Sum(column => column.Rows.Count));
        Assert.Empty(groups.Unmapped);
        Assert.Equal("customfield_10019", tab.Board?.RankFieldId);
    }

    [Fact]
    public async Task GivenCompanyManagedBacklog_WhenOpened_ThenTheBacklogEndpointIsUsedAndOnlyBacklogStatusesLoad()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("board-gcm-config"))
                .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("rows-gcm-backlog")));

        var state = await OpenAsync(fixture, ViewSourceKind.Backlog, "4");

        var tab = state.Session.Tabs[0];
        Assert.Equal(12, tab.Rows.Count);
        Assert.Contains("/rest/agile/1.0/board/4/backlog", fixture.Path(1));

        var groups = GroupOf(tab);
        Assert.Equal(12, groups.Columns[0].Rows.Count);
        Assert.All(groups.Columns.Skip(1), column => Assert.Empty(column.Rows));
    }

    [Fact]
    public async Task GivenTeamManagedBacklog_WhenOpened_ThenItListsWhatTheEndpointReturnsNotOnlyTheBacklogColumn()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("board-gtm-config"))
                .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("rows-gtm-backlog")));

        var state = await OpenAsync(fixture, ViewSourceKind.Backlog, "3");

        var tab = state.Session.Tabs[0];
        Assert.Equal(13, tab.Rows.Count);
        Assert.Contains("/rest/agile/1.0/board/3/backlog", fixture.Path(1));
        Assert.Empty(GroupOf(tab).Unmapped);
    }

    [Fact]
    public async Task GivenBoardOfThreePages_WhenOpened_ThenPagesAreWalkedByStartAtUntilTotalIsReached()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("board-gcm-config"))
                .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("rows-gcm-issues-page1"))
                .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("rows-gcm-issues-page2"))
                .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("rows-gcm-issues-page3-last")));

        var state = await OpenAsync(fixture, ViewSourceKind.Board, "4");

        var tab = state.Session.Tabs[0];
        Assert.Equal(24, tab.Rows.Count);
        Assert.Null(tab.Next);
        Assert.Equal(4, fixture.Http.Requests.Count);
        Assert.Contains("startAt=0", fixture.Query(1));
        Assert.Contains("startAt=10", fixture.Query(2));
        Assert.Contains("startAt=20", fixture.Query(3));
        Assert.Single(fixture.Http.Requests, uri => uri.AbsolutePath.EndsWith("configuration", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GivenUnknownBoard_WhenOpened_ThenTheErrorShowsWithNoRowsAndNoIssueRequestIsMade()
    {
        using var fixture = await ConnectedAsync(http => http.Reply(HttpStatusCode.NotFound, NotFound));

        var state = await OpenAsync(fixture, ViewSourceKind.Board, "99");

        var tab = state.Session.Tabs[0];
        var failed = Assert.IsType<Failed>(tab.Load);
        Assert.Equal(JiraErrorKind.NotFound, failed.Kind);
        Assert.Equal("Not found, or you have no access to it.", failed.Message);
        Assert.Empty(tab.Rows);
        Assert.Null(tab.Board);
        Assert.Single(fixture.Http.Requests);
    }

    [Fact]
    public async Task GivenIssuesRequestFails_WhenOpened_ThenConfigurationIsKeptAndTheErrorMapsThroughTheTable()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("board-gcm-config"))
                .Reply(HttpStatusCode.Forbidden, NotFound));

        var state = await OpenAsync(fixture, ViewSourceKind.Board, "4");

        var tab = state.Session.Tabs[0];
        Assert.Equal(JiraErrorKind.Forbidden, Assert.IsType<Failed>(tab.Load).Kind);
        Assert.NotNull(tab.Board);
        Assert.Empty(tab.Rows);
    }

    [Fact]
    public async Task GivenBoardIdThatIsNotANumber_WhenOpened_ThenTheTabFailsWithoutAnyRequest()
    {
        using var fixture = await ConnectedAsync(_ => { });

        var state = await OpenAsync(fixture, ViewSourceKind.Board, "abc");

        Assert.IsType<Failed>(state.Session.Tabs[0].Load);
        Assert.Empty(fixture.Http.Requests);
    }

    [Fact]
    public async Task GivenConfigurationInFlight_WhenTheProfileIsSwitched_ThenNeitherConfigurationNorIssuesArePosted()
    {
        var gate = new TaskCompletionSource();

        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("board-gcm-config"), gate.Task)
                .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("rows-gcm-issues")));

        var reducer = new AppReducer(DefaultKeymap.Load());
        var opened = reducer.Reduce(Start(), new ViewOpenRequested(Definition(ViewSourceKind.Board, "4")));
        var effect = Assert.IsType<LoadViewPageEffect>(Assert.Single(opened.Effects));

        var run = fixture.Handler.StartAsync(effect);

        // The switch has to land while the request is in flight, not before the background task has sent it.
        await fixture.Http.GateReached.WaitAsync(TimeSpan.FromSeconds(30));
        await fixture.Connections.ActivateAsync(_b, 2, CancellationToken.None);
        gate.SetResult();
        await run;

        Assert.Empty(fixture.Posted);
    }

    [Fact]
    public async Task GivenNoConnection_WhenOpeningABoard_ThenTheTabIsToldSoInsteadOfLoadingForever()
    {
        var connections = new SiteConnections(
            new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => null)));
        var posted = new List<Command>();

        using var runner = new NetworkRunner(connections, new ListCommandSink(posted), CancellationToken.None);
        var handler = new ViewLoadHandler(runner);
        var reducer = new AppReducer(DefaultKeymap.Load());
        var opened = reducer.Reduce(Start(), new ViewOpenRequested(Definition(ViewSourceKind.Board, "4")));

        await handler.StartAsync(Assert.IsType<LoadViewPageEffect>(Assert.Single(opened.Effects)));

        var failed = Assert.IsType<ViewLoadFailed>(Assert.Single(posted));
        Assert.Contains("Not connected", LoadErrorText.For(failed.Error));
    }

    private static BoardColumnGroups GroupOf(TabState tab)
    {
        Assert.NotNull(tab.Board);

        return BoardColumns.Group(tab.Board, tab.Rows);
    }

    private static AppState Start()
    {
        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_a), Session = SiteSession.Empty(_a) };
    }

    private static ViewDefinition Definition(ViewSourceKind source, string id)
    {
        return new ViewDefinition(source, id, ViewLayout.Columns, "Board");
    }

    private static Task<AppState> OpenAsync(Fixture fixture, ViewSourceKind source, string id)
    {
        return RunAsync(fixture, Start(), new ViewOpenRequested(Definition(source, id)));
    }

    /// <summary>Feeds <paramref name="command"/> to the reducer and every effect and reply that follows.</summary>
    private static async Task<AppState> RunAsync(Fixture fixture, AppState start, Command command)
    {
        var reducer = new AppReducer(DefaultKeymap.Load());
        var pending = new Queue<Command>([command]);
        var state = start;

        while (pending.Count > 0)
        {
            var result = reducer.Reduce(state, pending.Dequeue());

            state = result.State;
            fixture.Posted.Clear();

            foreach (var effect in result.Effects.OfType<LoadViewPageEffect>())
            {
                await fixture.Handler.StartAsync(effect);
            }

            foreach (var posted in fixture.Posted)
            {
                pending.Enqueue(posted);
            }
        }

        return state;
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
            Handler = new ViewLoadHandler(Runner);
        }

        public SiteConnections Connections { get; }

        public ScriptedJiraHandler Http { get; }

        public NetworkRunner Runner { get; }

        public ViewLoadHandler Handler { get; }

        public List<Command> Posted { get; } = [];

        public string Path(int index)
        {
            return Http.Requests.ElementAt(index).AbsolutePath;
        }

        public string Query(int index)
        {
            return Uri.UnescapeDataString(Http.Requests.ElementAt(index).Query);
        }

        public void Dispose()
        {
            Runner.Dispose();
            Connections.Dispose();
        }
    }
}
