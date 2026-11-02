using Geeratya.Core.Profiles;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;
using Geeratya.App.Tests.Fakes;

namespace Geeratya.App.Tests;

public sealed class SiteConnectionsTests
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly Profile _dev = new("dev", "https://dev.atlassian.net", "me@dev.test");

    [Fact]
    public async Task GivenStoredToken_WhenActivating_ThenTransportIsBoundToProfileSiteAndAccount()
    {
        var store = await StoreWithAsync(_work);

        using var connections = Create(store);

        var failure = await connections.ActivateAsync(_work, profileCount: 1, CancellationToken.None);

        Assert.Null(failure);
        Assert.Equal(new Uri(_work.SiteUrl), connections.Current!.Connection.SiteUrl);
        Assert.Equal("me@work.test", connections.Current.Connection.AccountEmail);
    }

    [Fact]
    public async Task GivenNoToken_WhenActivating_ThenFailureNamesTheProfileAndHasNoTransport()
    {
        using var connections = Create(new FakeSecretStore());

        var failure = await connections.ActivateAsync(_work, profileCount: 1, CancellationToken.None);

        Assert.Equal(_work, failure!.Profile);
        Assert.Contains(TokenProvider.EnvironmentVariable, failure.Message);
        Assert.Null(connections.Current);
    }

    [Fact]
    public async Task GivenFailedProfile_WhenActivatingItAgain_ThenNoSecondFailureIsReported()
    {
        using var connections = Create(new FakeSecretStore());

        await connections.ActivateAsync(_work, profileCount: 1, CancellationToken.None);
        var again = await connections.ActivateAsync(_work, profileCount: 1, CancellationToken.None);

        Assert.Null(again);
    }

    [Fact]
    public async Task GivenFailedProfile_WhenProfileCountChanges_ThenItIsTriedAgain()
    {
        using var connections = Create(new FakeSecretStore());

        await connections.ActivateAsync(_work, profileCount: 1, CancellationToken.None);
        var again = await connections.ActivateAsync(_work, profileCount: 2, CancellationToken.None);

        Assert.Equal(_work, again!.Profile);
    }

    [Fact]
    public async Task GivenFailedProfile_WhenRetryRequested_ThenItFailsAgainOnce()
    {
        using var connections = Create(new FakeSecretStore());

        await connections.ActivateAsync(_work, profileCount: 1, CancellationToken.None);
        connections.RetryFailed();
        var again = await connections.ActivateAsync(_work, profileCount: 1, CancellationToken.None);

        Assert.Equal(_work, again!.Profile);
    }

    [Fact]
    public async Task GivenEnvironmentTokenAndSeveralProfiles_WhenActivating_ThenTokenIsNotUsedAndStatusSaysToStoreOne()
    {
        using var connections = new SiteConnections(
            new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => "env-token")));

        var failure = await connections.ActivateAsync(_dev, profileCount: 2, CancellationToken.None);

        Assert.Equal(_dev, failure!.Profile);
        Assert.DoesNotContain(TokenProvider.EnvironmentVariable, failure.Message);
        Assert.Contains("stored", failure.Message);
        Assert.DoesNotContain("env-token", failure.Message);
        Assert.Null(connections.Current);
    }

    [Fact]
    public async Task GivenFailedProfile_WhenActivatedAgainWithOneProfile_ThenEnvironmentTokenConnects()
    {
        using var connections = new SiteConnections(
            new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => "env-token")));

        await connections.ActivateAsync(_dev, profileCount: 2, CancellationToken.None);
        var failure = await connections.ActivateAsync(_dev, profileCount: 1, CancellationToken.None);

        Assert.Null(failure);
        Assert.NotNull(connections.Current);
    }

    [Fact]
    public async Task GivenEnvironmentTokenAndOneProfile_WhenActivating_ThenTokenIsUsed()
    {
        using var connections = new SiteConnections(
            new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => "env-token")));

        var failure = await connections.ActivateAsync(_dev, profileCount: 1, CancellationToken.None);

        Assert.Null(failure);
        Assert.NotNull(connections.Current);
    }

    [Fact]
    public async Task GivenBlankEnvironmentToken_WhenActivating_ThenItIsReportedAsMissingToken()
    {
        using var connections = new SiteConnections(
            new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => "  ")));

        var failure = await connections.ActivateAsync(_work, profileCount: 1, CancellationToken.None);

        Assert.Contains(TokenProvider.EnvironmentVariable, failure!.Message);
    }

    [Fact]
    public async Task GivenBrokenSecretStore_WhenActivating_ThenFailureIsReportedNotThrown()
    {
        using var connections = new SiteConnections(
            new TokenProvider(new ThrowingSecretStore(), new EnvironmentReader(_ => null)));

        var failure = await connections.ActivateAsync(_work, profileCount: 1, CancellationToken.None);

        Assert.Contains("secret store failed", failure!.Message);
    }

    [Fact]
    public async Task GivenPlainHttpSite_WhenActivating_ThenFailureIsReported()
    {
        var insecure = new Profile("bad", "http://jira.example.com", "me@x.test");
        var store = await StoreWithAsync(insecure);

        using var connections = Create(store);

        var failure = await connections.ActivateAsync(insecure, 1, CancellationToken.None);

        Assert.NotNull(failure);
        Assert.Null(connections.Current);
    }

    [Fact]
    public async Task GivenConnectedProfile_WhenSwitching_ThenNewTransportReplacesTheOld()
    {
        var store = await StoreWithAsync(_work, _dev);

        using var connections = Create(store);

        await connections.ActivateAsync(_work, profileCount: 1, CancellationToken.None);

        var before = connections.Generation;

        await connections.ActivateAsync(_dev, profileCount: 1, CancellationToken.None);

        Assert.Equal(new Uri(_dev.SiteUrl), connections.Current!.Connection.SiteUrl);
        Assert.False(connections.IsCurrent(before));
    }

    [Fact]
    public async Task GivenSameProfile_WhenActivatingAgain_ThenNothingChanges()
    {
        var store = await StoreWithAsync(_work);

        using var connections = Create(store);

        await connections.ActivateAsync(_work, profileCount: 1, CancellationToken.None);

        var transport = connections.Current;
        var generation = connections.Generation;

        await connections.ActivateAsync(_work, profileCount: 1, CancellationToken.None);

        Assert.Same(transport, connections.Current);
        Assert.True(connections.IsCurrent(generation));
    }

    [Fact]
    public async Task GivenConnectedProfile_WhenDroppingIt_ThenTransportIsGoneButOtherDropIsIgnored()
    {
        var store = await StoreWithAsync(_work);

        using var connections = Create(store);

        await connections.ActivateAsync(_work, profileCount: 1, CancellationToken.None);
        connections.Drop(_dev);

        Assert.NotNull(connections.Current);

        connections.Drop(_work);

        Assert.Null(connections.Current);
    }

    [Fact]
    public async Task GivenSlowTokenLookup_WhenSwitchingMeanwhile_ThenTheLateOldConnectionIsDiscarded()
    {
        var store = new GatedSecretStore();

        using var connections = new SiteConnections(new TokenProvider(store, new EnvironmentReader(_ => null)));

        var slow = connections.ActivateAsync(_work, profileCount: 1, CancellationToken.None);

        await store.Started.Task;

        var fast = connections.ActivateAsync(_dev, profileCount: 1, CancellationToken.None);

        store.Release(_dev.CredentialKey, "token-dev");
        await fast;
        store.Release(_work.CredentialKey, "token-work");
        await slow;

        Assert.Equal(new Uri(_dev.SiteUrl), connections.Current!.Connection.SiteUrl);
    }

    private static SiteConnections Create(FakeSecretStore store)
    {
        return new SiteConnections(new TokenProvider(store, new EnvironmentReader(_ => null)));
    }

    private static async Task<FakeSecretStore> StoreWithAsync(params Profile[] profiles)
    {
        var store = new FakeSecretStore();

        foreach (var profile in profiles)
        {
            await store.SetAsync(profile.CredentialKey, $"token-{profile.Name}", CancellationToken.None);
        }

        return store;
    }

    private sealed class ThrowingSecretStore : ISecretStore
    {
        public bool IsAvailable => true;

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken)
        {
            throw new SecretStoreException("keyring locked");
        }

        public Task<bool> SetAsync(string key, string secret, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class GatedSecretStore : ISecretStore
    {
        private readonly Dictionary<string, TaskCompletionSource<string?>> _pending = [];
        private readonly object _lock = new();

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsAvailable => true;

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken)
        {
            Task<string?> task;

            lock (_lock)
            {
                task = Pending(key).Task;
            }

            Started.TrySetResult();

            return task;
        }

        public Task<bool> SetAsync(string key, string secret, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public void Release(string key, string secret)
        {
            lock (_lock)
            {
                Pending(key).TrySetResult(secret);
            }
        }

        private TaskCompletionSource<string?> Pending(string key)
        {
            if (!_pending.TryGetValue(key, out var source))
            {
                source = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pending[key] = source;
            }

            return source;
        }
    }
}
