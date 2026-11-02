using System.Net;
using Geeratya.App.Tests.Fakes;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
using Geeratya.Jira.Transport;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;

namespace Geeratya.App.Tests;

[Trait("Os", "Independent")]
public sealed class SuggestionHandlerTests
{
    private static readonly Profile _profile = new("a", "https://a.atlassian.net", "me@a.test");
    private static readonly TabId _tab = new(1);
    private static readonly SuggestionSource _assignee = new(SuggestionKind.AssignableUsers, "A-1", []);

    [Fact]
    public async Task GivenAUserQuery_WhenTheDebounceHasNotPassed_ThenNothingIsSentUntilItDoes()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Users()));

        var task = fixture.Handler.StartAsync(Effect(1, "jo"));
        await fixture.WaitForTimersAsync(1);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(249));

        Assert.Empty(fixture.Http.Requests);
        Assert.False(task.IsCompleted);

        fixture.Time.Advance(TimeSpan.FromMilliseconds(1));
        await task;

        var loaded = Assert.IsType<SuggestionsLoaded>(Assert.Single(fixture.Posted));
        var request = Assert.Single(fixture.Http.Requests);
        Assert.Equal(1, loaded.RequestId);
        Assert.Equal("fake-account-1", Assert.Single(loaded.Items).Id);
        Assert.EndsWith("/rest/api/3/user/assignable/search", request.AbsolutePath);
        Assert.Contains("issueKey=A-1", request.Query);
        Assert.Contains("query=jo", request.Query);
    }

    [Fact]
    public async Task GivenTwoQueriesInARow_WhenTheSecondStarts_ThenTheFirstIsCancelledAndOnlyOneRequestIsSent()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Users()));

        var first = fixture.Handler.StartAsync(Effect(1, "j"));
        await fixture.WaitForTimersAsync(1);
        var second = fixture.Handler.StartAsync(Effect(2, "jo"));
        await first;
        await fixture.WaitForTimersAsync(1);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(250));
        await second;

        var loaded = Assert.IsType<SuggestionsLoaded>(Assert.Single(fixture.Posted));
        Assert.Equal(2, loaded.RequestId);
        Assert.Single(fixture.Http.Requests);
    }

    [Fact]
    public async Task GivenAQueryWaitingForTheDebounce_WhenTheTabCancels_ThenNoRequestIsSentAndNothingIsPosted()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Users()));

        var task = fixture.Handler.StartAsync(Effect(1, "jo"));
        await fixture.WaitForTimersAsync(1);
        fixture.Handler.Cancel(_tab);
        await task;

        Assert.Empty(fixture.Http.Requests);
        Assert.Empty(fixture.Posted);
    }

    [Fact]
    public async Task GivenALookupThatTakesLong_WhenTheSlowTimeIsReached_ThenSlowIsPostedBeforeTheAnswer()
    {
        var gate = new TaskCompletionSource();
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Users(), gate.Task));

        var task = fixture.Handler.StartAsync(Effect(1, "jo"));
        await fixture.WaitForTimersAsync(1);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(250));
        await fixture.WaitForTimersAsync(1);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(150));
        await fixture.WaitForPostedAsync(1);

        Assert.IsType<SuggestionsSlow>(Assert.Single(fixture.Posted));

        gate.SetResult();
        await task;

        Assert.IsType<SuggestionsLoaded>(fixture.Posted[1]);
    }

    [Fact]
    public async Task GivenAFastLookup_WhenItAnswers_ThenNoSlowNoticeIsPosted()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, Users()));

        var task = fixture.Handler.StartAsync(Effect(1, "jo"));
        await fixture.WaitForTimersAsync(1);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(250));
        await task;

        Assert.DoesNotContain(fixture.Posted, command => command is SuggestionsSlow);
    }

    [Fact]
    public async Task GivenAServerError_WhenTheLookupFails_ThenTheFailureCarriesTheRequestId()
    {
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.InternalServerError, "{}"));

        var task = fixture.Handler.StartAsync(Effect(5, "jo"));
        await fixture.WaitForTimersAsync(1);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(250));
        await task;

        var failed = Assert.IsType<SuggestionsFailed>(Assert.Single(fixture.Posted));
        Assert.Equal(5, failed.RequestId);
    }

    [Fact]
    public async Task GivenALabelQuery_WhenTheSiteAnswers_ThenKnownAndSiteLabelsAreBothOffered()
    {
        var site = """{"values":["backend-site"],"isLast":true}""";
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, site));
        var source = new SuggestionSource(SuggestionKind.Labels, null, ["backend-view"]);

        var task = fixture.Handler.StartAsync(new SearchSuggestionsEffect(_tab, 3, source, "backend", TimeSpan.FromMilliseconds(250)));
        await fixture.WaitForTimersAsync(1);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(250));
        await task;

        var loaded = Assert.IsType<SuggestionsLoaded>(Assert.Single(fixture.Posted));
        Assert.Equal(["backend-site", "backend-view"], loaded.Items.Select(item => item.Label).Order());
    }

    [Fact]
    public async Task GivenAParentQuery_WhenLookedUp_ThenTheIssuePickerIsAsked()
    {
        var picker = RecordedFixtures.Read("suggestions/issue-picker.json");
        using var fixture = await ConnectedAsync(h => h.Reply(HttpStatusCode.OK, picker));
        var source = new SuggestionSource(SuggestionKind.Issues, "A-1", []);

        var task = fixture.Handler.StartAsync(new SearchSuggestionsEffect(_tab, 4, source, "login", TimeSpan.FromMilliseconds(250)));
        await fixture.WaitForTimersAsync(1);
        fixture.Time.Advance(TimeSpan.FromMilliseconds(250));
        await task;

        Assert.IsType<SuggestionsLoaded>(Assert.Single(fixture.Posted));
        Assert.EndsWith("/rest/api/3/issue/picker", Assert.Single(fixture.Http.Requests).AbsolutePath);
    }

    private static SearchSuggestionsEffect Effect(long requestId, string query)
    {
        return new SearchSuggestionsEffect(_tab, requestId, _assignee, query, TimeSpan.FromMilliseconds(250));
    }

    private static string Users()
    {
        return RecordedFixtures.Read("suggestions/recorded-users-assignable-gcm.json");
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
            Handler = new SuggestionHandler(Runner, Time);
        }

        public SiteConnections Connections { get; }

        public ScriptedJiraHandler Http { get; }

        public NetworkRunner Runner { get; }

        public ManualTimeProvider Time { get; } = new();

        public SuggestionHandler Handler { get; }

        public List<Command> Posted { get; } = [];

        /// <summary>Waits (bounded, no fixed sleep) until the handler has armed <paramref name="count"/> timers.</summary>
        public async Task WaitForTimersAsync(int count)
        {
            await WaitUntilAsync(() => Time.PendingTimers >= count);
        }

        public async Task WaitForPostedAsync(int count)
        {
            await WaitUntilAsync(() => Posted.Count >= count);
        }

        public void Dispose()
        {
            Runner.Dispose();
            Connections.Dispose();
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            while (!condition())
            {
                await Task.Delay(1, limit.Token);
            }
        }
    }
}
