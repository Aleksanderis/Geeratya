using System.Collections.Concurrent;
using System.Net;
using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;
using Geeratya.Tui.Tests.Fakes;

namespace Geeratya.Tui.Tests;

/// <summary>
/// The issue detail through the real path: Enter in the reducer, the load handler against the recorded fixtures
/// served by an in-process Jira, and the replies back through the reducer.
/// </summary>
public sealed class IssueDetailFlowTests : IDisposable
{
    private const string Site = "/rest/api/3/issue";

    private static readonly Profile _profile = new("a", "https://a.atlassian.net", "me@a.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());
    private readonly RoutedJiraHandler _http = new();
    private readonly ConcurrentQueue<Command> _posted = new();
    private SiteConnections? _connections;
    private NetworkRunner? _runner;
    private IssueDetailLoadHandler? _handler;

    public void Dispose()
    {
        _runner?.Dispose();
        _connections?.Dispose();
    }

    [Fact]
    public async Task GivenTeamManagedIssueWithRichAdf_WhenOpened_ThenDescriptionAndCommentsAreFormattedAndThreeRequestsWereMade()
    {
        _http.Fixture($"{Site}/GTM-5", "adf/issue-gtm5-rich-description.json")
            .Fixture($"{Site}/GTM-5/comment", "adf/comments-gtm5.json")
            .Fixture($"{Site}/GTM-5/remotelink", "links/remotelink-list-gtm5.json");
        await ConnectAsync();

        var state = await OpenAsync("GTM-5");

        var detail = Top(state);
        var text = Text(detail);
        Assert.Contains("Heading one", text);
        Assert.Contains("Survey4 task with rich ADF", text);
        Assert.Contains("Comments (2)", text);
        Assert.Contains("Test User 1", text);
        Assert.Equal(LoadState.Idle, detail.IssueLoad);
        Assert.Equal(LoadState.Idle, detail.CommentsLoad);
        Assert.Equal(LoadState.Idle, detail.LinksLoad);
        Assert.NotNull(detail.RemoteLinks);
        Assert.Equal(
            [$"{Site}/GTM-5", $"{Site}/GTM-5/comment", $"{Site}/GTM-5/remotelink"],
            _http.Requests.Select(uri => uri.AbsolutePath).Order());
    }

    [Fact]
    public async Task GivenCompanyManagedIssueWithComments_WhenOpened_ThenTheNoDescriptionTextAndAllCommentsShow()
    {
        _http.Fixture($"{Site}/GCM-6", "issues/issue-gcm6.json")
            .Fixture($"{Site}/GCM-6/comment", "comments/comments-gcm6.json")
            .Fixture($"{Site}/GCM-6/remotelink", "links/remotelink-list-gcm6.json");
        await ConnectAsync();

        var state = await OpenAsync("GCM-6");

        var text = Text(Top(state));
        Assert.Contains("No description.", text);
        Assert.Contains("Parent: GCM-1", text);
        Assert.Contains("Comments (3)", text);
        Assert.Contains("Written by an inactive account", text);
        Assert.Contains("żółć 日本語", text);
    }

    [Fact]
    public async Task GivenCompanyManagedIssueWithMediaDescription_WhenOpened_ThenMediaShowsAsAMarkerNotAnImage()
    {
        _http.Fixture($"{Site}/GCM-24", "issues/issue-gcm24.json")
            .Fixture($"{Site}/GCM-24/comment", "comments/comments-empty.json")
            .Fixture($"{Site}/GCM-24/remotelink", "links/remotelink-list-empty.json");
        await ConnectAsync();

        var state = await OpenAsync("GCM-24");

        var text = Text(Top(state));
        Assert.Contains("[image]", text);
        Assert.Contains("No comments.", text);
    }

    [Fact]
    public async Task GivenIssueNotFound_WhenOpened_ThenTheSameNotFoundTextShowsAndTheListIsStillUnderneath()
    {
        _http.Fixture($"{Site}/GCM-99", "errors/issue-404.json", HttpStatusCode.NotFound)
            .Fixture($"{Site}/GCM-99/comment", "errors/issue-404.json", HttpStatusCode.NotFound)
            .Fixture($"{Site}/GCM-99/remotelink", "errors/issue-404.json", HttpStatusCode.NotFound);
        await ConnectAsync();

        var state = await OpenAsync("GCM-99");

        var tab = Tab(state);
        Assert.Contains("Not found or no access: GCM-99", Text(tab.Detail[0]));
        Assert.Single(tab.Rows);

        var back = await PumpAsync(state, new KeyPressed("Esc"));

        Assert.Empty(Tab(back).Detail);
    }

    [Fact]
    public async Task GivenRateLimitedJira_WhenOpened_ThenTheRateLimitTextShows()
    {
        _http.Fixture($"{Site}/GCM-6", "errors/search-429-rate-limited.json", HttpStatusCode.TooManyRequests)
            .Fixture($"{Site}/GCM-6/comment", "errors/search-429-rate-limited.json", HttpStatusCode.TooManyRequests)
            .Fixture($"{Site}/GCM-6/remotelink", "errors/search-429-rate-limited.json", HttpStatusCode.TooManyRequests);
        await ConnectAsync();

        var state = await OpenAsync("GCM-6");

        Assert.Contains("rate limiting", Text(Top(state)));
    }

    [Fact]
    public async Task GivenUnauthorizedJira_WhenOpened_ThenTheTokenHintShows()
    {
        _http.Fixture($"{Site}/GCM-6", "errors/myself-401-bad-token.json", HttpStatusCode.Unauthorized)
            .Fixture($"{Site}/GCM-6/comment", "errors/myself-401-bad-token.json", HttpStatusCode.Unauthorized)
            .Fixture($"{Site}/GCM-6/remotelink", "errors/myself-401-bad-token.json", HttpStatusCode.Unauthorized);
        await ConnectAsync();

        var state = await OpenAsync("GCM-6");

        Assert.Contains("Check the token", Text(Top(state)));
    }

    [Fact]
    public async Task GivenNoConnection_WhenOpened_ThenTheDetailSaysSoInsteadOfLoadingForever()
    {
        _connections = new SiteConnections(new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => null)));
        _runner = new NetworkRunner(_connections, _posted.Enqueue, CancellationToken.None);
        _handler = new IssueDetailLoadHandler(_runner);

        var state = await OpenAsync("GCM-6");

        var detail = Top(state);
        Assert.IsType<Failed>(detail.IssueLoad);
        Assert.Contains("Not connected to Jira", Text(detail));
    }

    [Fact]
    public async Task GivenRepliesInFlight_WhenTheTabIsClosed_ThenNothingIsPostedForIt()
    {
        var gate = new TaskCompletionSource();

        _http.Fixture($"{Site}/GCM-6", "issues/issue-gcm6.json", gate: gate.Task)
            .Fixture($"{Site}/GCM-6/comment", "comments/comments-gcm6.json", gate: gate.Task)
            .Fixture($"{Site}/GCM-6/remotelink", "links/remotelink-list-gcm6.json", gate: gate.Task);
        await ConnectAsync();
        var opened = _reducer.Reduce(ListState("GCM-6"), new KeyPressed("Enter"));
        var runs = opened.Effects.Select(effect => _handler!.StartAsync(effect)).ToList();
        var tab = Tab(opened.State);

        _runner!.CancelScope(tab.Id.Value);
        gate.SetResult();
        await Task.WhenAll(runs);

        Assert.Empty(_posted);
    }

    private static TabState Tab(AppState state)
    {
        return state.Session.Tabs.Single(tab => tab.Id == state.Session.Active);
    }

    private static DetailState Top(AppState state)
    {
        return Tab(state).Detail[^1];
    }

    private static string Text(DetailState detail)
    {
        return string.Join('\n', detail.Document!.Lines.Select(line => line.PlainText));
    }

    private AppState ListState(string key)
    {
        var profiles = ProfileCatalog.Empty.With(_profile);
        var state = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(_profile) };

        state = _reducer.Reduce(state, new ViewportChanged(20, 100)).State;

        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");
        var opened = _reducer.Reduce(state, new ViewOpenRequested(definition));
        var tab = Tab(opened.State);
        var row = new IssueSummary(
            "1",
            key,
            "Row summary",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
        var page = new IssuePage([row], null, null);

        return _reducer.Reduce(
            opened.State,
            new ViewPageLoaded(tab.Id, tab.RequestId, page, DateTimeOffset.UtcNow)).State;
    }

    private async Task<AppState> OpenAsync(string key)
    {
        return await PumpAsync(ListState(key), new KeyPressed("Enter"));
    }

    private async Task<AppState> PumpAsync(AppState state, Command command)
    {
        var pending = new Queue<Command>([command]);

        while (pending.Count > 0)
        {
            var result = _reducer.Reduce(state, pending.Dequeue());

            state = result.State;
            _posted.Clear();

            var runs = result.Effects.Select(effect => _handler!.StartAsync(effect)).ToList();

            await Task.WhenAll(runs);

            foreach (var posted in _posted)
            {
                pending.Enqueue(posted);
            }
        }

        return state;
    }

    private async Task ConnectAsync()
    {
        var store = new FakeSecretStore();
        var options = new JiraTransportOptions
        {
            MaxRetries = 0,
            Delay = (_, _) => Task.CompletedTask,
            Jitter = () => 0.5,
        };

        await store.SetAsync(_profile.CredentialKey, "token-a", CancellationToken.None);

        _connections = new SiteConnections(
            new TokenProvider(store, new EnvironmentReader(_ => null)),
            (connection, token) => new JiraTransport(connection, token, _http, options));

        await _connections.ActivateAsync(_profile, 1, CancellationToken.None);

        _runner = new NetworkRunner(_connections, _posted.Enqueue, CancellationToken.None);
        _handler = new IssueDetailLoadHandler(_runner);
    }
}
