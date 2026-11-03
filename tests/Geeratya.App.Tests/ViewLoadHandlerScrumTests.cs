using System.Net;
using Geeratya.App.Tests.Fakes;
using Geeratya.Core.Backlog;
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
    private const string ActiveAndFutureSprints =
        """{"maxResults":50,"startAt":0,"isLast":true,"values":[{"id":7,"state":"active","name":"Sprint 1","originBoardId":6},{"id":8,"state":"future","name":"Sprint 2","originBoardId":6}]}""";

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
    public async Task GivenSprintSource_WhenOpened_ThenOnlyThatSprintIsAskedWithSprintAndEstimateFields()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-gcs-config.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/sprint-gcs-active.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/field-list.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-issues-gcs.json")));

        var state = await OpenAsync(fixture, ViewSourceKind.Sprint, "6:7");

        Assert.Equal("/rest/agile/1.0/sprint/7", fixture.Path(1));
        Assert.Equal("/rest/agile/1.0/board/6/issue", fixture.Path(3));
        Assert.Contains("jql=sprint = 7", Uri.UnescapeDataString(fixture.Query(3)));
        Assert.Contains("customfield_10020", fixture.Query(3));
        Assert.Equal(LoadState.Idle, state.Session.Tabs[0].Load);
        Assert.NotEmpty(state.Session.Tabs[0].Rows);
        Assert.Equal("Seed Sprint 2", state.Session.Tabs[0].Sprint?.Name);
        Assert.True(state.Session.Tabs[0].SprintResolved);
    }

    [Fact]
    public async Task GivenActiveSprintSource_WhenTheBoardHasNoActiveSprint_ThenTheTabIsResolvedWithoutASprint()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-gcs-config.json"))
                .Reply(HttpStatusCode.OK, SprintPage)
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/field-list.json"))
                .Reply(HttpStatusCode.OK, """{"startAt":0,"maxResults":50,"total":0,"issues":[]}"""));

        var state = await OpenAsync(fixture, ViewSourceKind.Sprint, "6:active");

        Assert.Contains("state=active", fixture.Query(1));
        Assert.Null(state.Session.Tabs[0].Sprint);
        Assert.True(state.Session.Tabs[0].SprintResolved);
    }

    [Fact]
    public async Task GivenActiveSprintSource_WhenTheSprintLookupFails_ThenTheIssuesStillLoad()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-gcs-config.json"))
                .Reply(HttpStatusCode.Forbidden, "{}")
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/field-list.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-issues-gcs.json")));

        var state = await OpenAsync(fixture, ViewSourceKind.Sprint, "6:active");

        Assert.Equal(LoadState.Idle, state.Session.Tabs[0].Load);
        Assert.NotEmpty(state.Session.Tabs[0].Rows);
        Assert.Null(state.Session.Tabs[0].Sprint);
        Assert.False(state.Session.Tabs[0].SprintResolved);
    }

    [Fact]
    public async Task GivenActiveSprintSource_WhenOpened_ThenOpenSprintsAreAsked()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-gcs-config.json"))
                .Reply(HttpStatusCode.OK, SprintPage)
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/field-list.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-issues-gcs.json")));

        await OpenAsync(fixture, ViewSourceKind.Sprint, "6:active");

        Assert.Contains("jql=sprint in openSprints() and sprint not in futureSprints()", Uri.UnescapeDataString(fixture.Query(3)));
    }

    [Fact]
    public async Task GivenScrumBacklogSource_WhenOpened_ThenOnlyOpenAndFutureSprintsAndTheBacklogAreAsked()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-gcs-config.json"))
                .Reply(HttpStatusCode.OK, ActiveAndFutureSprints)
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/field-list.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-issues-gcs.json")));

        var state = await OpenAsync(fixture, ViewSourceKind.ScrumBacklog, "6");

        Assert.Equal("/rest/agile/1.0/board/6/issue", fixture.Path(3));
        Assert.Contains(
            "jql=sprint in openSprints() or sprint in futureSprints() or sprint is EMPTY",
            Uri.UnescapeDataString(fixture.Query(3)));
        Assert.Contains("customfield_10020", fixture.Query(3));
        Assert.Equal(["Sprint 1", "Sprint 2"], state.Session.Tabs[0].Sprints.Select(sprint => sprint.Name));
    }

    [Theory]
    [InlineData("scrum/board-gcs-config.json", "scrum/board-issues-gcs.json", "6")]
    [InlineData("scrum/board-gts-config.json", "scrum/board-issues-gts.json", "5")]
    public async Task GivenEitherProjectKind_WhenTheScrumBacklogIsOpened_ThenRowsAreGroupedIntoContiguousSections(
        string configuration,
        string issues,
        string board)
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read(configuration))
                .Reply(HttpStatusCode.OK, ActiveAndFutureSprints)
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/field-list.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read(issues)));

        var state = await OpenAsync(fixture, ViewSourceKind.ScrumBacklog, board);

        var tab = state.Session.Tabs[0];
        var layout = BacklogSections.Build(tab);
        Assert.Equal(tab.Rows.Count, layout.Sections.Sum(section => section.RowCount));
        Assert.Equal(BacklogSections.BacklogKey, layout.Sections[^1].Key);
        Assert.Equal(layout.Sections.Count, layout.Sections.Select(section => section.Key).Distinct().Count());
        Assert.Same(tab.Rows, BacklogSections.Order(tab.Rows));
    }

    [Fact]
    public async Task GivenScrumBacklogSource_WhenTheSprintListFails_ThenTheIssuesStillLoadWithoutSprintMetadata()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-gcs-config.json"))
                .Reply(HttpStatusCode.Forbidden, "{}")
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/field-list.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-issues-gcs.json")));

        var state = await OpenAsync(fixture, ViewSourceKind.ScrumBacklog, "6");

        var tab = state.Session.Tabs[0];
        Assert.Equal(LoadState.Idle, tab.Load);
        Assert.Empty(tab.Sprints);
        Assert.Equal(19, tab.Rows.Count);
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

    [Fact]
    public async Task GivenTeamManagedScrumBoard_WhenBacklogIsOpened_ThenTabBecomesScrumBacklogWithSections()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-gts-config.json"))
                .Reply(HttpStatusCode.OK, SprintPage)
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/sprints-gts.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("metadata/field-list.json"))
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("scrum/board-issues-gts.json")));

        var state = await OpenAsync(fixture, ViewSourceKind.Backlog, "5");

        var tab = state.Session.Tabs[0];
        Assert.Equal(ViewSourceKind.ScrumBacklog, tab.Definition.Source);
        Assert.True(BacklogSections.Applies(tab));
        Assert.NotEmpty(tab.Sprints);
        Assert.Contains("jql=", fixture.Query(4));
        Assert.Equal("/rest/agile/1.0/board/5/issue", fixture.Path(4));
        Assert.Equal(BoardKind.Scrum, fixture.Services.BoardKinds.Recall(5));
    }

    [Fact]
    public async Task GivenTeamManagedKanbanBoard_WhenBacklogIsOpened_ThenItKeepsThePlainBacklog()
    {
        using var fixture = await ConnectedAsync(
            http => http
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("boards/board-gtm-config.json"))
                .Reply(HttpStatusCode.BadRequest, NoSprints)
                .Reply(HttpStatusCode.OK, RecordedFixtures.Read("boards/rows-gtm-issues.json")));

        var state = await OpenAsync(fixture, ViewSourceKind.Backlog, "3");

        Assert.Equal(ViewSourceKind.Backlog, state.Session.Tabs[0].Definition.Source);
        Assert.Equal("/rest/agile/1.0/board/3/backlog", fixture.Path(2));
        Assert.Empty(state.Session.Tabs[0].Sprints);
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
