using System.Net;
using System.Text.Json;
using Geeratya.App.Tests.Fakes;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Links;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;

namespace Geeratya.App.Tests;

[Trait("Os", "Independent")]
public sealed class WriteHandlerLinkTests
{
    private static readonly Profile _a = new("a", "https://a.atlassian.net", "me@a.test");

    [Fact]
    public async Task GivenALinkRequest_WhenExecuting_ThenOnePostNamesTheSidesAndNoReadFollows()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.Created, string.Empty));
        var request = new LinkIssuesRequest("A-1", "A-2", "10000", IssueLinkDirection.Inward, "is blocked by");

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(41, request));

        var succeeded = Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted));
        Assert.Empty(succeeded.Rows);
        Assert.Equal(["POST"], fixture.Http.Methods);
        Assert.EndsWith("/rest/api/3/issueLink", Assert.Single(fixture.Http.Requests).AbsolutePath);
        using var body = JsonDocument.Parse(Assert.Single(fixture.Http.Bodies));
        Assert.Equal("A-2", body.RootElement.GetProperty("outwardIssue").GetProperty("key").GetString());
        Assert.Equal("A-1", body.RootElement.GetProperty("inwardIssue").GetProperty("key").GetString());
    }

    [Fact]
    public async Task GivenAnUnlinkRequest_WhenExecuting_ThenOneDeleteOfThatLinkAndNoReadFollows()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.NoContent, string.Empty));
        var request = new UnlinkIssuesRequest("A-1", "A-2", "10004", IssueLinkDirection.Inward, "is blocked by");

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(41, request));

        Assert.Empty(Assert.IsType<WriteSucceeded>(Assert.Single(fixture.Posted)).Rows);
        Assert.Equal(["DELETE"], fixture.Http.Methods);
        Assert.EndsWith("/rest/api/3/issueLink/10004", Assert.Single(fixture.Http.Requests).AbsolutePath);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task GivenARefusedLink_WhenExecuting_ThenItFailsDefinitelyAndIsSentOnce(HttpStatusCode status)
    {
        using var fixture = await ConnectedAsync(h => h.Reply(status, "{}"));
        var request = new LinkIssuesRequest("A-1", "A-2", "10000", IssueLinkDirection.Outward, "blocks");

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(41, request));

        Assert.IsType<WriteFailed>(Assert.Single(fixture.Posted));
        Assert.Single(fixture.Http.Requests);
    }

    [Fact]
    public async Task GivenALostReply_WhenExecuting_ThenTheLinkIsNotRepeated()
    {
        using var fixture = await ConnectedAsync(
            h => h.Reply(HttpStatusCode.BadGateway, string.Empty).Reply(HttpStatusCode.Created, string.Empty),
            3);
        var request = new LinkIssuesRequest("A-1", "A-2", "10000", IssueLinkDirection.Outward, "blocks");

        await fixture.Writes.StartAsync(new ExecuteWriteEffect(41, request));

        Assert.IsType<WriteOutcomeUnknown>(Assert.Single(fixture.Posted));
        Assert.Single(fixture.Http.Requests);
    }

    [Theory]
    [InlineData("GTM-12", IssueLinkDirection.Inward, "is blocked by", VerifyOutcome.Applied)]
    [InlineData("GTM-12", IssueLinkDirection.Outward, "blocks", VerifyOutcome.NotApplied)]
    [InlineData("GTM-99", IssueLinkDirection.Inward, "is blocked by", VerifyOutcome.NotApplied)]
    public async Task GivenALostCreateReply_WhenVerifying_ThenTheIssueLinksDecideTheOutcome(
        string other,
        IssueLinkDirection direction,
        string text,
        VerifyOutcome outcome)
    {
        var issue = RecordedFixtures.Read("issues/issue-gtm11.json");
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, issue));
        var request = new LinkIssuesRequest("GTM-11", other, "10000", direction, text);

        await fixture.Writes.StartAsync(new VerifyWriteEffect(41, request));

        Assert.Equal(outcome, Assert.IsType<WriteVerified>(Assert.Single(fixture.Posted)).Outcome);
        Assert.EndsWith("/rest/api/3/issue/GTM-11", Assert.Single(fixture.Http.Requests).AbsolutePath);
    }

    [Theory]
    [InlineData("10004", VerifyOutcome.NotApplied)]
    [InlineData("10999", VerifyOutcome.Applied)]
    public async Task GivenALostRemoveReply_WhenVerifying_ThenAPresentLinkMeansItWasNotRemoved(
        string linkId,
        VerifyOutcome outcome)
    {
        var issue = RecordedFixtures.Read("issues/issue-gtm11.json");
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, issue));
        var request = new UnlinkIssuesRequest("GTM-11", "GTM-12", linkId, IssueLinkDirection.Inward, "is blocked by");

        await fixture.Writes.StartAsync(new VerifyWriteEffect(41, request));

        Assert.Equal(outcome, Assert.IsType<WriteVerified>(Assert.Single(fixture.Posted)).Outcome);
    }

    [Fact]
    public async Task GivenAFailingRead_WhenVerifying_ThenTheOutcomeIsUnreachableNotApplied()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.InternalServerError, string.Empty));
        var request = new LinkIssuesRequest("GTM-11", "GTM-12", "10000", IssueLinkDirection.Inward, "is blocked by");

        await fixture.Writes.StartAsync(new VerifyWriteEffect(41, request));

        Assert.Equal(VerifyOutcome.Unreachable, Assert.IsType<WriteVerified>(Assert.Single(fixture.Posted)).Outcome);
    }

    [Fact]
    public async Task GivenTheLinkTypesOfTheSite_WhenTheLinkFormLoads_ThenEachSideIsAChoice()
    {
        var types = RecordedFixtures.Read("links/issuelink-types.json");
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, types));

        await fixture.Metadata.StartAsync(new LoadFormMetadataEffect(new TabId(1), 7, new LinkFormTarget("A-1")));

        var loaded = Assert.IsType<FormMetadataLoaded>(Assert.Single(fixture.Posted));
        Assert.Equal(7, loaded.RequestId);
        string[] labels =
        [
            "blocks", "is blocked by", "clones", "is cloned by", "duplicates", "is duplicated by", "causes",
            "is caused by", "relates to",
        ];
        Assert.Equal(labels, loaded.Fields[0].Options.Select(option => option.Label));
        Assert.EndsWith("/rest/api/3/issueLinkType", Assert.Single(fixture.Http.Requests).AbsolutePath);
    }

    [Fact]
    public async Task GivenASiteWithoutLinkTypes_WhenTheLinkFormLoads_ThenTheFormFailsWithTheReason()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, """{"issueLinkTypes":[]}"""));

        await fixture.Metadata.StartAsync(new LoadFormMetadataEffect(new TabId(1), 7, new LinkFormTarget("A-1")));

        var failed = Assert.IsType<FormMetadataFailed>(Assert.Single(fixture.Posted));
        Assert.Contains("no issue link types", failed.Message);
    }

    [Fact]
    public async Task GivenReducerAndHandlers_WhenLinkingAnIssue_ThenTheFormLoadsTheTypesAndThePostIsSent()
    {
        var types = RecordedFixtures.Read("links/issuelink-types.json");
        using var fixture = await ConnectedAsync(h => h
            .Reply(HttpStatusCode.OK, types)
            .Reply(HttpStatusCode.Created, string.Empty));
        var reducer = new AppReducer(DefaultKeymap.Load());

        var opened = PressAll(reducer, OpenTab(reducer), "Space", "i", "l");
        await fixture.Metadata.StartAsync(Assert.IsType<LoadFormMetadataEffect>(Assert.Single(opened.Effects)));
        var loaded = reducer.Reduce(opened.State, Assert.Single(fixture.Posted));
        var filled = PressAll(reducer, loaded.State, "Enter", "a", "-", "2");
        var submitted = PressAll(reducer, filled.State, "Ctrl-s");
        await fixture.Writes.StartAsync(Assert.IsType<ExecuteWriteEffect>(Assert.Single(submitted.Effects)));
        var finished = reducer.Reduce(submitted.State, fixture.Posted[1]);

        Assert.Equal(["GET", "POST"], fixture.Http.Methods);
        using var body = JsonDocument.Parse(fixture.Http.Bodies.Last());
        Assert.Equal("A-1", body.RootElement.GetProperty("outwardIssue").GetProperty("key").GetString());
        Assert.Equal("A-2", body.RootElement.GetProperty("inwardIssue").GetProperty("key").GetString());
        Assert.Equal("10000", body.RootElement.GetProperty("type").GetProperty("id").GetString());
        Assert.Equal("Linked A-1 blocks A-2", finished.State.Message);
        Assert.Null(finished.State.Session.Tabs[0].Form);
        Assert.Empty(finished.State.Session.Operations);
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

        var page = new IssuePage([row], null, null);

        return reducer.Reduce(opened, new ViewPageLoaded(tab.Id, tab.RequestId, page, DateTimeOffset.UnixEpoch)).State;
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
            Writes = new WriteHandler(Runner);
            Metadata = new FormMetadataHandler(Runner);
        }

        public SiteConnections Connections { get; }

        public ScriptedJiraHandler Http { get; }

        public NetworkRunner Runner { get; }

        public WriteHandler Writes { get; }

        public FormMetadataHandler Metadata { get; }

        public List<Command> Posted { get; } = [];

        public void Dispose()
        {
            Runner.Dispose();
            Connections.Dispose();
        }
    }
}
