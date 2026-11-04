using System.Net;
using Geeratya.App.Tests.Fakes;
using Geeratya.Core.Boards;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Settings;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Gateway;
using Geeratya.Jira.Transport;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;

namespace Geeratya.App.Tests;

/// <summary>
/// The Done cutoff of a Kanban board load (#681): the tab's choice, else Jira's own value through the gateway, else
/// the setting. Through the reducer, the load handler and a scripted Jira that serves the recorded gateway replies.
/// </summary>
public sealed class ViewLoadHandlerCutoffTests
{
    private const string NullCutoff = """{"data":{"node":{"completedIssueSearchCutOffInDays":null}}}""";
    private const string SevenDays = """{"data":{"node":{"completedIssueSearchCutOffInDays":7}}}""";
    private const string NoSprints = """{"errorMessages":["The board does not support sprints"],"errors":{}}""";
    private const string TenantPath = "/_edge/tenant_info";
    private const string GraphQlPath = "/gateway/api/graphql";

    private static readonly Profile _a = new("a", "https://a.atlassian.net", "me@a.test");
    private static readonly Profile _b = new("b", "https://b.atlassian.net", "me@b.test");

    [Theory]
    [InlineData("board-gcm-config", "rows-gcm-issues", "4", "gcm4", false)]
    [InlineData("board-gtm-config", "rows-gtm-issues", "3", "gtm3", true)]
    public async Task GivenJiraSaysFourteenDays_WhenAKanbanBoardOpens_ThenItWinsOverTheSettingForEitherProjectKind(
        string configuration,
        string rows,
        string board,
        string recorded,
        bool probes)
    {
        using var fixture = await ConnectedAsync(
            http =>
            {
                http.Reply(HttpStatusCode.OK, BoardFixtureFiles.Read(configuration));

                if (probes)
                {
                    http.Reply(HttpStatusCode.BadRequest, NoSprints);
                }

                http.Reply(HttpStatusCode.OK, BoardFixtureFiles.Read(rows));
                ScriptChain(http, recorded);
            });
        var start = Start() with { Settings = new AppSettings(DoneCutoffDays: 7) };

        var state = await RunAsync(fixture, start, new ViewOpenRequested(Definition(board)));

        var tab = state.Session.Tabs[0];
        Assert.Equal(14, tab.DoneCutoffDays);
        Assert.Equal(DoneCutoffSource.Jira, tab.CutoffSource);
        Assert.Contains("statusCategoryChangedDate >= -14d", fixture.Query(probes ? 2 : 1));
        Assert.Equal("Done (14d)", ColumnsNavigation.ColumnsOf(tab)[^1].Name);
        Assert.Equal(
            [$"GET {TenantPath}", $"POST {GraphQlPath}", $"POST {GraphQlPath}"],
            fixture.Http.GatewayRequests.Select(request => $"{request.Method} {request.Path}"));
        Assert.Contains($"board/{board}", fixture.Http.GatewayRequests.ElementAt(1).Body);
    }

    [Fact]
    public async Task GivenJiraAppliesNoCutoff_WhenAKanbanBoardOpens_ThenEveryDoneIssueIsRequestedAndTheTitleIsPlain()
    {
        using var fixture = await ConnectedAsync(
            http =>
            {
                ScriptBoard(http);
                ScriptChain(http, "gcm4", NullCutoff);
            });

        var state = await RunAsync(fixture, Start(), new ViewOpenRequested(Definition("4")));

        var tab = state.Session.Tabs[0];
        Assert.Equal(0, tab.DoneCutoffDays);
        Assert.Equal(DoneCutoffSource.Jira, tab.CutoffSource);
        Assert.DoesNotContain("statusCategoryChangedDate", fixture.Query(1));
        Assert.Equal("Done", ColumnsNavigation.ColumnsOf(tab)[^1].Name);
        Assert.Equal(24, tab.Rows.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "{}")]
    [InlineData(HttpStatusCode.Forbidden, "{}")]
    [InlineData((HttpStatusCode)429, "{}")]
    [InlineData(HttpStatusCode.InternalServerError, "oops")]
    [InlineData(HttpStatusCode.NotFound, "")]
    [InlineData(HttpStatusCode.OK, "not json")]
    [InlineData(HttpStatusCode.OK, "{}")]
    [InlineData(HttpStatusCode.OK, """{"cloudId":""}""")]
    public async Task GivenTheTenantLookupFails_WhenAKanbanBoardOpens_ThenTheSettingAppliesAndTheBoardLoads(
        HttpStatusCode status,
        string body)
    {
        using var fixture = await ConnectedAsync(
            http =>
            {
                ScriptBoard(http);
                http.Gateway(status, body);
            });
        var start = Start() with { Settings = new AppSettings(DoneCutoffDays: 7) };

        var state = await RunAsync(fixture, start, new ViewOpenRequested(Definition("4")));

        AssertFellBack(state, fixture, 7);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "{}")]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.OK, """{"errors":[{"message":"nope"}]}""")]
    [InlineData(HttpStatusCode.OK, """{"data":{"node":{}}}""")]
    [InlineData(HttpStatusCode.OK, """{"data":{"node":{"completedIssueSearchCutOffInDays":"soon"}}}""")]
    public async Task GivenTheViewLookupFails_WhenAKanbanBoardOpens_ThenTheSettingAppliesAndTheBoardLoads(
        HttpStatusCode status,
        string body)
    {
        using var fixture = await ConnectedAsync(
            http =>
            {
                ScriptBoard(http);
                http.Gateway(HttpStatusCode.OK, GatewayFixtures.Read("tenant-info"))
                    .Gateway(HttpStatusCode.OK, GatewayFixtures.Read("navigation-gcm4"))
                    .Gateway(status, body);
            });
        var start = Start() with { Settings = new AppSettings(DoneCutoffDays: 21) };

        var state = await RunAsync(fixture, start, new ViewOpenRequested(Definition("4")));

        AssertFellBack(state, fixture, 21);
    }

    [Fact]
    public async Task GivenTheSettingIsAll_WhenJiraCannotBeRead_ThenNoCutoffIsRequested()
    {
        using var fixture = await ConnectedAsync(ScriptBoard);
        var start = Start() with { Settings = new AppSettings(DoneCutoffDays: 0) };

        var state = await RunAsync(fixture, start, new ViewOpenRequested(Definition("4")));

        Assert.DoesNotContain("statusCategoryChangedDate", fixture.Query(1));
        Assert.Equal(DoneCutoffSource.Setting, state.Session.Tabs[0].CutoffSource);
    }

    [Fact]
    public async Task GivenATabChoice_WhenAKanbanBoardOpens_ThenItWinsOverJiraAndNoGatewayRequestIsMade()
    {
        using var fixture = await ConnectedAsync(
            http =>
            {
                ScriptBoard(http);
                http.Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("board-gcm-config"));
                http.Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("rows-gcm-issues"));
                ScriptChain(http, "gcm4");
            });
        var opened = await RunAsync(fixture, Start(), new ViewOpenRequested(Definition("4")));

        var chosen = await RunLineAsync(fixture, opened, "donecutoff 1w");

        var tab = chosen.Session.Tabs[0];
        Assert.Equal(7, tab.DoneCutoffDays);
        Assert.Equal(DoneCutoffSource.Choice, tab.CutoffSource);
        Assert.Contains("statusCategoryChangedDate >= -7d", fixture.Query(3));
        Assert.Equal("Done (7d)", ColumnsNavigation.ColumnsOf(tab)[^1].Name);
        Assert.Equal(3, fixture.Http.GatewayRequests.Count);
    }

    [Fact]
    public async Task GivenTheChoiceAll_WhenJiraSaysFourteenDays_ThenEveryDoneIssueIsRequested()
    {
        using var fixture = await ConnectedAsync(
            http =>
            {
                ScriptBoard(http);
                http.Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("board-gcm-config"));
                http.Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("rows-gcm-issues"));
                ScriptChain(http, "gcm4");
            });
        var opened = await RunAsync(fixture, Start(), new ViewOpenRequested(Definition("4")));

        var chosen = await RunLineAsync(fixture, opened, "donecutoff all");

        Assert.DoesNotContain("statusCategoryChangedDate", fixture.Query(3));
        Assert.Equal("Done", ColumnsNavigation.ColumnsOf(chosen.Session.Tabs[0])[^1].Name);
        Assert.Equal(DoneCutoffSource.Choice, chosen.Session.Tabs[0].CutoffSource);
    }

    [Fact]
    public async Task GivenAChoiceThenFollowJira_WhenReloading_ThenJiraValueAppliesFromTheCache()
    {
        using var fixture = await ConnectedAsync(
            http =>
            {
                ScriptBoard(http);
                http.Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("board-gcm-config"));
                http.Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("rows-gcm-issues"));
                http.Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("board-gcm-config"));
                http.Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("rows-gcm-issues"));
                ScriptChain(http, "gcm4");
            });
        var opened = await RunAsync(fixture, Start(), new ViewOpenRequested(Definition("4")));
        var chosen = await RunLineAsync(fixture, opened, "donecutoff 1w");

        var followed = await RunLineAsync(fixture, chosen, "donecutoff auto");

        var tab = followed.Session.Tabs[0];
        Assert.Equal(14, tab.DoneCutoffDays);
        Assert.Equal(DoneCutoffSource.Jira, tab.CutoffSource);
        Assert.Equal(3, fixture.Http.GatewayRequests.Count);
    }

    [Fact]
    public async Task GivenTheCutoffWasRead_WhenTheBoardRefreshes_ThenJiraIsNotAskedAgain()
    {
        using var fixture = await ConnectedAsync(
            http =>
            {
                ScriptBoard(http);
                ScriptBoard(http);
                ScriptChain(http, "gcm4");
            });
        var opened = await RunAsync(fixture, Start(), new ViewOpenRequested(Definition("4")));

        var refreshed = await RunAsync(fixture, opened, new RefreshRequested(opened.Session.Tabs[0].Id));

        Assert.Equal(DoneCutoffSource.Jira, refreshed.Session.Tabs[0].CutoffSource);
        Assert.Equal(3, fixture.Http.GatewayRequests.Count);
        Assert.Contains("statusCategoryChangedDate >= -14d", fixture.Query(3));
    }

    [Fact]
    public async Task GivenAScrumBoard_WhenItOpens_ThenTheGatewayIsNotAsked()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-gcs-config.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/field-list.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-issues-gcs.json")));

        var state = await RunAsync(fixture, Start(), new ViewOpenRequested(Definition("6")));

        Assert.Empty(fixture.Http.GatewayRequests);
        Assert.Null(state.Session.Tabs[0].CutoffSource);
        Assert.DoesNotContain("statusCategoryChangedDate", fixture.Query(2));
    }

    [Fact]
    public async Task GivenAnotherProfile_WhenTheBoardOpensThere_ThenTheCutoffIsReadAgainForThatSite()
    {
        using var fixture = await ConnectedAsync(
            http =>
            {
                ScriptBoard(http);
                ScriptBoard(http);
                ScriptChain(http, "gcm4");
                ScriptChain(http, "gcm4", SevenDays);
            });
        var first = await RunAsync(fixture, Start(), new ViewOpenRequested(Definition("4")));
        await fixture.Connections.ActivateAsync(_b, 2, CancellationToken.None);

        var other = await RunAsync(fixture, first, new RefreshRequested(first.Session.Tabs[0].Id));

        Assert.Equal(6, fixture.Http.GatewayRequests.Count);
        Assert.Equal(7, other.Session.Tabs[0].DoneCutoffDays);
    }

    [Fact]
    public async Task GivenTheCachedCutoffExpiresBetweenPages_WhenABoardOfThreePagesLoads_ThenEveryPageUsesTheFirstPagesDays()
    {
        using var fixture = await ConnectedAsync(
            http =>
            {
                http.Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("board-gcm-config"))
                    .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("rows-gcm-issues-page1"))
                    .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("rows-gcm-issues-page2"))
                    .Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("rows-gcm-issues-page3-last"));
                ScriptChain(http, "gcm4");
                ScriptChain(http, "gcm4", SevenDays);
                ScriptChain(http, "gcm4", SevenDays);
            });

        // Each effect batch (one page) takes longer than the 5 minute lifetime of Jira's answer.
        var state = await RunAsync(
            fixture,
            Start() with { Settings = new AppSettings(DoneCutoffDays: 21) },
            () => fixture.Clock.Advance(DoneCutoffReader.ValueLifetime + TimeSpan.FromMinutes(1)),
            new ViewOpenRequested(Definition("4")));

        var tab = state.Session.Tabs[0];
        Assert.Equal(24, tab.Rows.Count);
        Assert.Equal(14, tab.DoneCutoffDays);
        Assert.Equal(DoneCutoffSource.Jira, tab.CutoffSource);
        Assert.Equal("Done (14d)", ColumnsNavigation.ColumnsOf(tab)[^1].Name);
        Assert.Contains("startAt=10", fixture.Query(2));
        Assert.Contains("statusCategoryChangedDate >= -14d", fixture.Query(1));
        Assert.Contains("statusCategoryChangedDate >= -14d", fixture.Query(2));
        Assert.Contains("statusCategoryChangedDate >= -14d", fixture.Query(3));
        Assert.Equal(3, fixture.Http.GatewayRequests.Count);
    }

    private static void AssertFellBack(AppState state, Fixture fixture, int days)
    {
        var tab = state.Session.Tabs[0];
        Assert.Equal(LoadState.Idle, tab.Load);
        Assert.Equal(24, tab.Rows.Count);
        Assert.Equal(days, tab.DoneCutoffDays);
        Assert.Equal(DoneCutoffSource.Setting, tab.CutoffSource);
        Assert.Contains($"statusCategoryChangedDate >= -{days}d", fixture.Query(1));
        Assert.Equal($"Done ({days}d)", ColumnsNavigation.ColumnsOf(tab)[^1].Name);
        Assert.Null(state.Message);
    }

    private static void ScriptBoard(ScriptedJiraHandler http)
    {
        http.Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("board-gcm-config"));
        http.Reply(HttpStatusCode.OK, BoardFixtureFiles.Read("rows-gcm-issues"));
    }

    private static void ScriptChain(ScriptedJiraHandler http, string recorded, string? cutoff = null)
    {
        http.Gateway(HttpStatusCode.OK, GatewayFixtures.Read("tenant-info"))
            .Gateway(HttpStatusCode.OK, GatewayFixtures.Read($"navigation-{recorded}"))
            .Gateway(HttpStatusCode.OK, cutoff ?? GatewayFixtures.Read($"cutoff-{recorded}"));
    }

    private static AppState Start()
    {
        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_a), Session = SiteSession.Empty(_a) };
    }

    private static ViewDefinition Definition(string id)
    {
        return new ViewDefinition(ViewSourceKind.Board, id, ViewLayout.Columns, "Board");
    }

    private static Task<AppState> RunLineAsync(Fixture fixture, AppState start, string line)
    {
        var typing = start with { Mode = InputMode.Command, PaletteText = line, PaletteBack = 0 };

        return RunAsync(fixture, typing, new KeyPressed("Enter"));
    }

    /// <summary>Feeds <paramref name="commands"/> to the reducer and every effect and reply that follows.</summary>
    private static Task<AppState> RunAsync(Fixture fixture, AppState start, params Command[] commands)
    {
        return RunAsync(fixture, start, () => { }, commands);
    }

    private static async Task<AppState> RunAsync(
        Fixture fixture,
        AppState start,
        Action afterPage,
        params Command[] commands)
    {
        var reducer = new AppReducer(DefaultKeymap.Load());
        var pending = new Queue<Command>(commands);
        var state = start;

        while (pending.Count > 0)
        {
            var result = reducer.Reduce(state, pending.Dequeue());

            state = result.State;
            fixture.Posted.Clear();

            foreach (var effect in result.Effects.OfType<LoadViewPageEffect>())
            {
                await fixture.Handler.StartAsync(effect);
                afterPage();
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

        var clock = new ManualTimeProvider();

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
            new FakeTransportFactory(http, options),
            clock: clock);

        await connections.ActivateAsync(_a, 2, CancellationToken.None);

        return new Fixture(connections, http, clock);
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture(SiteConnections connections, ScriptedJiraHandler http, ManualTimeProvider clock)
        {
            Clock = clock;
            Connections = connections;
            Http = http;
            Runner = new NetworkRunner(connections, new ListCommandSink(Posted), CancellationToken.None);
            Handler = new ViewLoadHandler(Runner);
        }

        public ManualTimeProvider Clock { get; }

        public SiteConnections Connections { get; }

        public ScriptedJiraHandler Http { get; }

        public NetworkRunner Runner { get; }

        public ViewLoadHandler Handler { get; }

        public List<Command> Posted { get; } = [];

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
