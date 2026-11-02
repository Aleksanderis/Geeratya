using System.Net;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Jira.Transport;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;
using Geeratya.Tui.Tests.Fakes;

namespace Geeratya.Tui.Tests;

[Trait("Os", "Independent")]
public sealed class ConnectionCheckHandlerTests : IDisposable
{
    private const string Myself =
        """{"accountId":"abc","displayName":"Test User 1","emailAddress":"me@a.test","active":true}""";

    private const string Unauthenticated = "Client must be authenticated to access this resource.";

    private static readonly Profile _a = new("a", "https://a.atlassian.net", "me@a.test");
    private static readonly Profile _b = new("b", "https://b.atlassian.net", "me@b.test");

    private readonly List<Command> _posted = [];
    private readonly List<IDisposable> _owned = [];

    public void Dispose()
    {
        foreach (var owned in _owned)
        {
            owned.Dispose();
        }
    }

    [Fact]
    public async Task GivenAcceptedCredentials_WhenChecking_ThenTheDisplayNameIsPosted()
    {
        var handler = await HandlerAsync(http => http.Reply(HttpStatusCode.OK, Myself));

        await handler.StartAsync(new CheckConnectionEffect(_a));

        var posted = Assert.IsType<ConnectionChecked>(Assert.Single(_posted));
        Assert.Equal("Test User 1", posted.User.DisplayName);
        Assert.Equal(_a, posted.Profile);
    }

    [Fact]
    public async Task GivenPlainTextUnauthorized_WhenChecking_ThenAnUnauthorizedFailureIsPosted()
    {
        var handler = await HandlerAsync(http => http.Reply(HttpStatusCode.Unauthorized, Unauthenticated));

        await handler.StartAsync(new CheckConnectionEffect(_a));

        var posted = Assert.IsType<ConnectionCheckFailed>(Assert.Single(_posted));
        Assert.Equal(JiraErrorKind.Unauthorized, posted.Error.Kind);
    }

    [Fact]
    public async Task GivenNoAnswerInTime_WhenChecking_ThenATimeoutFailureIsPosted()
    {
        var never = new TaskCompletionSource();
        var options = new JiraTransportOptions { Timeout = TimeSpan.FromMilliseconds(50), MaxRetries = 0 };
        var handler = await HandlerAsync(http => http.Reply(HttpStatusCode.OK, Myself, never.Task), options);

        await handler.StartAsync(new CheckConnectionEffect(_a));

        var posted = Assert.IsType<ConnectionCheckFailed>(Assert.Single(_posted));
        Assert.Equal(JiraErrorKind.Timeout, posted.Error.Kind);
    }

    [Fact]
    public async Task GivenHostNotAllowed_WhenChecking_ThenAHostRefusedFailureIsPosted()
    {
        var options = new JiraTransportOptions { AllowedHosts = new HashSet<string> { "other.example" } };
        var handler = await HandlerAsync(http => http.Reply(HttpStatusCode.OK, Myself), options);

        await handler.StartAsync(new CheckConnectionEffect(_a));

        var posted = Assert.IsType<ConnectionCheckFailed>(Assert.Single(_posted));
        Assert.Equal(JiraErrorKind.HostRefused, posted.Error.Kind);
    }

    [Fact]
    public async Task GivenCheckInFlight_WhenTheProfileIsSwitched_ThenNothingIsPosted()
    {
        var gate = new TaskCompletionSource();
        var connections = await ConnectionsAsync(http => http.Reply(HttpStatusCode.OK, Myself, gate.Task), null);
        var handler = HandlerFor(connections);

        var run = handler.StartAsync(new CheckConnectionEffect(_a));

        await connections.ActivateAsync(_b, 2, CancellationToken.None);
        gate.SetResult();
        await run;

        Assert.Empty(_posted);
    }

    [Fact]
    public async Task GivenNoToken_WhenChecking_ThenNothingIsPostedAndNoRequestIsSent()
    {
        var connections = new SiteConnections(new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => null)));

        _owned.Add(connections);

        var failure = await connections.ActivateAsync(_a, 2, CancellationToken.None);
        var handler = HandlerFor(connections);

        await handler.StartAsync(new CheckConnectionEffect(_a));

        Assert.NotNull(failure);
        Assert.Empty(_posted);
    }

    [Fact]
    public async Task GivenReducerAndFake_WhenAddingAProfile_ThenTheStatusReadsConnectedAsTestUser()
    {
        var handler = await HandlerAsync(http => http.Reply(HttpStatusCode.OK, Myself));
        var reducer = new AppReducer(DefaultKeymap.Load());
        var changed = new ProfilesChanged(ProfileCatalog.Empty.With(_a), "Added.");

        var added = reducer.Reduce(AppState.Initial, changed);
        var effect = Assert.IsType<CheckConnectionEffect>(Assert.Single(added.Effects));

        await handler.StartAsync(effect);

        var state = reducer.Reduce(added.State, Assert.Single(_posted)).State;

        Assert.Equal("Connected as Test User 1 on a.atlassian.net", ConnectionText.Line(state.Session));
    }

    private async Task<ConnectionCheckHandler> HandlerAsync(
        Action<ScriptedJiraHandler> script,
        JiraTransportOptions? options = null)
    {
        return HandlerFor(await ConnectionsAsync(script, options));
    }

    private ConnectionCheckHandler HandlerFor(SiteConnections connections)
    {
        var runner = new NetworkRunner(connections, _posted.Add, CancellationToken.None);

        _owned.Add(runner);

        return new ConnectionCheckHandler(connections, runner);
    }

    private async Task<SiteConnections> ConnectionsAsync(Action<ScriptedJiraHandler> script, JiraTransportOptions? options)
    {
        var http = new ScriptedJiraHandler();
        var store = new FakeSecretStore();

        script(http);
        await store.SetAsync(_a.CredentialKey, "token-a", CancellationToken.None);
        await store.SetAsync(_b.CredentialKey, "token-b", CancellationToken.None);

        var transportOptions = (options ?? new JiraTransportOptions()) with
        {
            MaxRetries = 0,
            Delay = (_, _) => Task.CompletedTask,
        };

        var connections = new SiteConnections(
            new TokenProvider(store, new EnvironmentReader(_ => null)),
            (connection, token) => new JiraTransport(connection, token, http, transportOptions));

        _owned.Add(connections);

        // Not awaited: the handler must wait for the connection itself.
        _ = connections.ActivateAsync(_a, 2, CancellationToken.None);

        return connections;
    }
}
