using System.Net;
using Geeratya.App.Tests.Fakes;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Transport;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;

namespace Geeratya.App.Tests;

public sealed class ViewLoadHandlerScrumTests
{
    private const string NoSprints = """{"errorMessages":["The board does not support sprints"],"errors":{}}""";
    private const string SprintPage = """{"maxResults":1,"startAt":0,"isLast":true,"values":[]}""";
    private const string OpenSprints = "sprint in openSprints()";

    private const string FirstPage =
        """{"startAt":0,"maxResults":1,"total":2,"issues":[{"id":"1","key":"GTS-1","fields":{"summary":"One"}}]}""";

    private const string SecondPage =
        """{"startAt":1,"maxResults":1,"total":2,"issues":[{"id":"2","key":"GTS-2","fields":{"summary":"Two"}}]}""";

    private static readonly Profile _profile = new("a", "https://a.atlassian.net", "me@a.test");

    [Fact]
    public async Task GivenCompanyManagedScrumBoard_WhenOpened_ThenOpenSprintsAreLoadedWithSprintEstimateAndEpic()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-gcs-config.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/field-list.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-issues-gcs.json")));

        var state = await OpenAsync(fixture, ViewSourceKind.Board, "6");

        var rows = state.Session.Tabs[0].Rows.ToDictionary(row => row.Key);
        Assert.Equal(3, fixture.Http.Requests.Count);
        Assert.Equal("/rest/api/3/field", fixture.Path(1));
        Assert.Equal("/rest/agile/1.0/board/6/issue", fixture.Path(2));
        Assert.Contains($"jql={OpenSprints}", fixture.Query(2));
        Assert.Contains("customfield_10020,customfield_10047,customfield_10016", fixture.Query(2));
        Assert.Equal(7, rows["GCS-8"].CurrentSprint?.Id);
        Assert.Equal(8.0, rows["GCS-8"].Estimate);
        Assert.Equal("GCS-2", rows["GCS-8"].Epic?.Key);
    }

    [Fact]
    public async Task GivenTeamManagedScrumBoard_WhenOpened_ThenItIsProbedOnceAndRoutedLikeScrum()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-gts-config.json"))
                .Reply(HttpStatusCode.OK, SprintPage)
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/field-list.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-issues-gts.json")));

        var state = await OpenAsync(fixture, ViewSourceKind.Board, "5");

        var rows = state.Session.Tabs[0].Rows.ToDictionary(row => row.Key);
        Assert.Equal("/rest/agile/1.0/board/5/sprint", fixture.Path(1));
        Assert.Contains($"jql={OpenSprints}", fixture.Query(3));
        Assert.Contains("customfield_10020,customfield_10016", fixture.Query(3));
        Assert.Equal(SprintState.Active, rows["GTS-32"].CurrentSprint?.State);
        Assert.Equal(3.0, rows["GTS-32"].Estimate);
        Assert.Equal(BoardKind.Scrum, fixture.Services.BoardKinds.Recall(5));
    }

    [Fact]
    public async Task GivenScrumBoardOfTwoPages_WhenContinued_ThenLaterPagesNeedNeitherProbeNorFieldList()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-gts-config.json"))
                .Reply(HttpStatusCode.OK, SprintPage)
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/field-list.json"))
                .Reply(HttpStatusCode.OK, FirstPage)
                .Reply(HttpStatusCode.OK, SecondPage));

        var state = await OpenAsync(fixture, ViewSourceKind.Board, "5");

        Assert.Equal(5, fixture.Http.Requests.Count);
        Assert.Contains("startAt=1", fixture.Query(4));
        Assert.Contains("customfield_10020", fixture.Query(4));
        Assert.Contains(OpenSprints, fixture.Query(4));
        Assert.Equal(["GTS-1", "GTS-2"], state.Session.Tabs[0].Rows.Select(row => row.Key));
    }

    [Fact]
    public async Task GivenScrumBacklog_WhenOpened_ThenTheBacklogEndpointIsAskedWithoutTheSprintFilter()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-gcs-config.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/field-list.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/backlog-gcs.json")));

        await OpenAsync(fixture, ViewSourceKind.Backlog, "6");

        Assert.Equal("/rest/agile/1.0/board/6/backlog", fixture.Path(2));
        Assert.DoesNotContain("jql=", fixture.Query(2));
        Assert.Contains("customfield_10020", fixture.Query(2));
    }

    [Fact]
    public async Task GivenSprintSource_WhenOpened_ThenOnlyThatSprintIsAskedWithSprintAndEstimateFields()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-gcs-config.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/field-list.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-issues-gcs.json")));

        var state = await OpenAsync(fixture, ViewSourceKind.Sprint, "6:7");

        Assert.Equal("/rest/agile/1.0/board/6/issue", fixture.Path(2));
        Assert.Contains("jql=sprint = 7", Uri.UnescapeDataString(fixture.Query(2)));
        Assert.Contains("customfield_10020", fixture.Query(2));
        Assert.Equal(LoadState.Idle, state.Session.Tabs[0].Load);
        Assert.NotEmpty(state.Session.Tabs[0].Rows);
    }

    [Fact]
    public async Task GivenActiveSprintSource_WhenOpened_ThenOpenSprintsAreAsked()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-gcs-config.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/field-list.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-issues-gcs.json")));

        await OpenAsync(fixture, ViewSourceKind.Sprint, "6:active");

        Assert.Contains($"jql={OpenSprints}", Uri.UnescapeDataString(fixture.Query(2)));
    }

    [Fact]
    public async Task GivenScrumBacklogSource_WhenOpened_ThenOnlyOpenAndFutureSprintsAndTheBacklogAreAsked()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-gcs-config.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/field-list.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-issues-gcs.json")));

        await OpenAsync(fixture, ViewSourceKind.ScrumBacklog, "6");

        Assert.Equal("/rest/agile/1.0/board/6/issue", fixture.Path(2));
        Assert.Contains(
            "jql=sprint in openSprints() or sprint in futureSprints() or sprint is EMPTY",
            Uri.UnescapeDataString(fixture.Query(2)));
        Assert.Contains("customfield_10020", fixture.Query(2));
    }

    [Fact]
    public async Task GivenMalformedSprintSource_WhenOpened_ThenTheTabFailsWithoutARequest()
    {
        using var fixture = await ConnectedAsync(_ => { });

        var state = await OpenAsync(fixture, ViewSourceKind.Sprint, "6");

        Assert.IsType<Failed>(state.Session.Tabs[0].Load);
        Assert.Empty(fixture.Http.Requests);
    }

    [Fact]
    public async Task GivenFieldListFails_WhenScrumBoardIsOpened_ThenRowsLoadWithoutSprintOrEstimate()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-gcs-config.json"))
                .Reply(HttpStatusCode.Forbidden, "{}")
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-issues-gcs.json")));

        var state = await OpenAsync(fixture, ViewSourceKind.Board, "6");

        var tab = state.Session.Tabs[0];
        Assert.Equal(LoadState.Idle, tab.Load);
        Assert.Equal(19, tab.Rows.Count);
        Assert.All(tab.Rows, row => Assert.Empty(row.Sprints));
        Assert.DoesNotContain("customfield", fixture.Query(2));
    }

    [Fact]
    public async Task GivenKindProbeFails_WhenSimpleBoardIsOpened_ThenItLoadsAsBeforeAndNothingIsRemembered()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("boards/board-gtm-config.json"))
                .Reply(HttpStatusCode.Forbidden, "{}")
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("boards/rows-gtm-issues.json")));

        var state = await OpenAsync(fixture, ViewSourceKind.Board, "3");

        Assert.Equal(16, state.Session.Tabs[0].Rows.Count);
        Assert.Equal(3, fixture.Http.Requests.Count);
        Assert.DoesNotContain("jql=", fixture.Query(2));
        Assert.Null(fixture.Services.BoardKinds.Recall(3));
    }

    [Fact]
    public async Task GivenKanbanAnswer_WhenSimpleBoardIsOpened_ThenNoFieldListIsRead()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("boards/board-gtm-config.json"))
                .Reply(HttpStatusCode.BadRequest, NoSprints)
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("boards/rows-gtm-issues.json")));

        await OpenAsync(fixture, ViewSourceKind.Board, "3");

        Assert.DoesNotContain(
            fixture.Http.Requests,
            uri => uri.AbsolutePath.EndsWith("/field", StringComparison.Ordinal));
        Assert.DoesNotContain("customfield", fixture.Query(2));
        Assert.Equal(BoardKind.Kanban, fixture.Services.BoardKinds.Recall(3));
    }

    private static ViewDefinition Definition(ViewSourceKind source, string id)
    {
        return new ViewDefinition(source, id, ViewLayout.Columns, "Board");
    }

    private static AppState Start()
    {
        return AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(_profile),
            Session = SiteSession.Empty(_profile),
        };
    }

    private static async Task<AppState> OpenAsync(Fixture fixture, ViewSourceKind source, string id)
    {
        var reducer = new AppReducer(DefaultKeymap.Load());
        var pending = new Queue<Command>([new ViewOpenRequested(Definition(source, id))]);
        var state = Start();

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
        await store.SetAsync(_profile.CredentialKey, "token-a", CancellationToken.None);

        var options = new JiraTransportOptions
        {
            MaxRetries = 0,
            Delay = (_, _) => Task.CompletedTask,
            Jitter = () => 0.5,
        };

        var connections = new SiteConnections(
            new TokenProvider(store, new EnvironmentReader(_ => null)),
            new FakeTransportFactory(http, options));

        await connections.ActivateAsync(_profile, 1, CancellationToken.None);

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

        public JiraServices Services => JiraServices.Create(Connections)!;

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
