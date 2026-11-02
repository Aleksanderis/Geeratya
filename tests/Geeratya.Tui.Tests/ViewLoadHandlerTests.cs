using System.Net;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Transport;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;
using Geeratya.Tui.Tests.Fakes;

namespace Geeratya.Tui.Tests;

public sealed class ViewLoadHandlerTests
{
    private const string PageOne =
        """{"issues":[{"id":"1","key":"A-1","fields":{"summary":"One"}},{"id":"2","key":"A-2","fields":{"summary":"Two"}}],"nextPageToken":"t2","isLast":false}""";

    private const string PageTwo = """{"issues":[{"id":"3","key":"A-3","fields":{"summary":"Three"}}],"isLast":true}""";
    private const string Empty = """{"issues":[],"isLast":true}""";
    private const string BadJql = """{"errorMessages":["Error in the JQL Query: bad"],"errors":{}}""";

    private static readonly Profile _a = new("a", "https://a.atlassian.net", "me@a.test");
    private static readonly Profile _b = new("b", "https://b.atlassian.net", "me@b.test");
    private static readonly TabId _tab = new(7);

    [Fact]
    public async Task GivenTwoPages_WhenLoadingFirstThenContinuing_ThenPagesComeInOrderWithTheRequestId()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, PageOne).Reply(HttpStatusCode.OK, PageTwo));

        await fixture.Handler.StartAsync(Load("project = A", null, 11));
        var first = Assert.IsType<ViewPageLoaded>(Assert.Single(fixture.Posted));
        await fixture.Handler.StartAsync(Load("project = A", first.Page.Next, 12));

        var second = Assert.IsType<ViewPageLoaded>(fixture.Posted[1]);
        Assert.Equal(["A-1", "A-2"], first.Page.Items.Select(issue => issue.Key));
        Assert.Equal(11, first.RequestId);
        Assert.Equal(_tab, first.Tab);
        Assert.Equal("t2", first.Page.Next!.Token);
        Assert.Equal(["A-3"], second.Page.Items.Select(issue => issue.Key));
        Assert.Equal(12, second.RequestId);
        Assert.Null(second.Page.Next);
        Assert.DoesNotContain("nextPageToken", fixture.Requested(0));
        Assert.Contains("nextPageToken=t2", fixture.Requested(1));
    }

    [Fact]
    public async Task GivenTypedJql_WhenLoading_ThenItIsSentAsTypedWithThePageSize()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Empty));

        await fixture.Handler.StartAsync(Load("project = A ORDER BY created DESC", null, 1));

        Assert.Contains("jql=project = A ORDER BY created DESC", fixture.Requested(0));
        Assert.Contains($"maxResults={ViewPaging.PageSize}", fixture.Requested(0));
    }

    [Fact]
    public async Task GivenSavedFilterSource_WhenLoading_ThenTheSearchIsFilterEqualsTheId()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Empty));
        var effect = Load("ignored", null, 1) with
        {
            Definition = new ViewDefinition(ViewSourceKind.SavedFilter, "10001", ViewLayout.List, "My filter"),
        };

        await fixture.Handler.StartAsync(effect);

        Assert.Contains("jql=filter = 10001", fixture.Requested(0));
    }

    [Fact]
    public async Task GivenEmptyResult_WhenLoading_ThenAnEmptyLastPageIsPosted()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Empty));

        await fixture.Handler.StartAsync(Load("project = NONE", null, 1));

        var loaded = Assert.IsType<ViewPageLoaded>(Assert.Single(fixture.Posted));
        Assert.Empty(loaded.Page.Items);
        Assert.Null(loaded.Page.Next);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, JiraErrorKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, JiraErrorKind.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, JiraErrorKind.NotFound)]
    [InlineData(HttpStatusCode.BadRequest, JiraErrorKind.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError, JiraErrorKind.ServerError)]
    public async Task GivenErrorStatus_WhenLoading_ThenTheFailureCarriesItsKindAndTheRequestId(
        HttpStatusCode status,
        JiraErrorKind kind)
    {
        using var fixture = await ConnectedAsync(h => h.Reply(status, BadJql));

        await fixture.Handler.StartAsync(Load("project = A", null, 5));

        var failed = Assert.IsType<ViewLoadFailed>(Assert.Single(fixture.Posted));
        Assert.Equal(kind, failed.Error.Kind);
        Assert.Equal(5, failed.RequestId);
        Assert.Equal(_tab, failed.Tab);
    }

    [Fact]
    public async Task GivenJqlSyntaxError_WhenLoading_ThenTheTabShowsJirasMessage()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.BadRequest, BadJql));

        await fixture.Handler.StartAsync(Load("project =", null, 1));

        var failed = Assert.IsType<ViewLoadFailed>(Assert.Single(fixture.Posted));
        Assert.Contains("Error in the JQL Query: bad", LoadErrorText.For(failed.Error));
    }

    [Fact]
    public async Task GivenRateLimit_WhenLoading_ThenTheFailureIsRateLimitedWithTheWait()
    {
        using var fixture = await ConnectedAsync(h => h.RateLimit("42"), maxRetries: 0);

        await fixture.Handler.StartAsync(Load("project = A", null, 1));

        var failed = Assert.IsType<ViewLoadFailed>(Assert.Single(fixture.Posted));
        Assert.Equal(JiraErrorKind.RateLimited, failed.Error.Kind);
        Assert.Contains("42 s", LoadErrorText.For(failed.Error));
    }

    [Fact]
    public async Task GivenTokenRejectedOnContinuedSearch_WhenLoading_ThenTheFailureIsPageExpired()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.BadRequest, BadJql));

        await fixture.Handler.StartAsync(Load("project = A", new("old-token"), 3));

        var failed = Assert.IsType<ViewLoadFailed>(Assert.Single(fixture.Posted));
        Assert.Equal(JiraErrorKind.PageExpired, failed.Error.Kind);
    }

    [Fact]
    public async Task GivenNoConnection_WhenLoading_ThenTheTabIsToldAtOnceInsteadOfLoadingForever()
    {
        using var fixture = Disconnected();

        await fixture.Handler.StartAsync(Load("project = A", null, 9));

        var failed = Assert.IsType<ViewLoadFailed>(Assert.Single(fixture.Posted));
        Assert.Equal(JiraErrorKind.Unexpected, failed.Error.Kind);
        Assert.Equal(9, failed.RequestId);
        Assert.Contains("Not connected", LoadErrorText.For(failed.Error));
    }

    [Theory]
    [InlineData(ViewSourceKind.Board)]
    [InlineData(ViewSourceKind.Backlog)]
    public async Task GivenBoardSource_WhenLoading_ThenItFailsAsNotSupportedYetWithoutACall(ViewSourceKind source)
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Empty));
        var effect = Load("1", null, 2) with { Definition = new ViewDefinition(source, "1", ViewLayout.Columns, "B") };

        await fixture.Handler.StartAsync(effect);

        var failed = Assert.IsType<ViewLoadFailed>(Assert.Single(fixture.Posted));
        Assert.Contains("cannot be loaded yet", failed.Error.Message);
        Assert.Empty(fixture.Http.Requests);
    }

    [Fact]
    public async Task GivenLoadInFlight_WhenTheTabIsClosed_ThenTheReplyIsNeverPosted()
    {
        var gate = new TaskCompletionSource();

        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, PageOne, gate.Task));

        var run = fixture.Handler.StartAsync(Load("project = A", null, 1));

        fixture.Runner.CancelScope(_tab.Value);
        gate.SetResult();
        await run;

        Assert.Empty(fixture.Posted);
    }

    [Fact]
    public async Task GivenLoadInFlight_WhenTheProfileIsSwitched_ThenTheReplyIsNeverPosted()
    {
        var gate = new TaskCompletionSource();

        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, PageOne, gate.Task));

        var run = fixture.Handler.StartAsync(Load("project = A", null, 1));

        await fixture.Connections.ActivateAsync(_b, 2, CancellationToken.None);
        gate.SetResult();
        await run;

        Assert.Empty(fixture.Posted);
    }

    [Fact]
    public async Task GivenReducerAndHandler_WhenOpeningAJqlViewAndScrollingToTheEnd_ThenAllPagesLoadInOrder()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, PageOne).Reply(HttpStatusCode.OK, PageTwo));
        var reducer = new AppReducer(DefaultKeymap.Load());
        var state = AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_a), Session = SiteSession.Empty(_a) };

        state = await PumpAsync(fixture, reducer, state, new ViewportChanged(2));
        state = await PumpAsync(
            fixture,
            reducer,
            state,
            new ViewOpenRequested(new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "A")));

        var tab = state.Session.Tabs[0];

        Assert.Equal(["A-1", "A-2", "A-3"], tab.Rows.Select(issue => issue.Key));
        Assert.Null(tab.Next);
        Assert.Equal(LoadState.Idle, tab.Load);
    }

    private static async Task<AppState> PumpAsync(Fixture fixture, AppReducer reducer, AppState state, Command command)
    {
        var pending = new Queue<Command>([command]);

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

    private static LoadViewPageEffect Load(string jql, PageCursor? cursor, long requestId)
    {
        var definition = new ViewDefinition(ViewSourceKind.Jql, jql, ViewLayout.List, jql);
        var reason = cursor is null ? LoadReason.Open : LoadReason.NextPage;

        return new LoadViewPageEffect(_tab, definition, cursor, requestId, reason, false);
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
        await store.SetAsync(_b.CredentialKey, "token-b", CancellationToken.None);

        var options = new JiraTransportOptions
        {
            MaxRetries = maxRetries,
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
            Handler = new ViewLoadHandler(Runner);
        }

        public SiteConnections Connections { get; }

        public ScriptedJiraHandler Http { get; }

        public NetworkRunner Runner { get; }

        public ViewLoadHandler Handler { get; }

        public List<Command> Posted { get; } = [];

        public string Requested(int index)
        {
            return Uri.UnescapeDataString(Http.Requests.ElementAt(index).Query.Replace('+', ' '));
        }

        public void Dispose()
        {
            Runner.Dispose();
            Connections.Dispose();
        }
    }
}
