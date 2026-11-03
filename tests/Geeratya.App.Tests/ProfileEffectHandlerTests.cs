using Geeratya.Core.BoardKinds;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Core.Cache;
using Geeratya.Core.History;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;
using Geeratya.App.Tests.Fakes;

namespace Geeratya.App.Tests;

public sealed class ProfileEffectHandlerTests : IDisposable
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly Profile _dev = new("dev", "https://dev.atlassian.net", "me@dev.test");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"geeratya-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task GivenAvailableStore_WhenAdding_ThenTokenIsStoredAndCatalogSaved()
    {
        var store = new FakeSecretStore();
        var fixture = Create(store);
        var handler = fixture.Handler;
        var profiles = fixture.Profiles;
        var catalog = ProfileCatalog.Empty.With(_work);

        var outcome = await handler.RunAsync(
            new AddProfileEffect(_work, new SecretText("hunter2-secret"), catalog),
            CancellationToken.None);

        var changed = Assert.IsType<ProfilesChanged>(outcome);

        Assert.Equal("hunter2-secret", store.Secrets[_work.CredentialKey]);
        Assert.Equal("work", profiles.Store.Load().Catalog.ActiveName);
        Assert.DoesNotContain("hunter2", changed.Message);
    }

    [Fact]
    public async Task GivenNoStore_WhenAdding_ThenProfileIsSavedAndMessageSaysToUseTheVariable()
    {
        var fixture = Create(new FakeSecretStore(isAvailable: false));
        var handler = fixture.Handler;
        var profiles = fixture.Profiles;

        var outcome = await handler.RunAsync(
            new AddProfileEffect(_work, new SecretText("hunter2-secret"), ProfileCatalog.Empty.With(_work)),
            CancellationToken.None);

        var changed = Assert.IsType<ProfilesChanged>(outcome);

        Assert.Contains("GEERATYA_TOKEN", changed.Message);
        Assert.Single(profiles.Store.Load().Catalog.Profiles);
    }

    [Fact]
    public async Task GivenFailingStore_WhenAdding_ThenFailsWithoutSavingTheProfile()
    {
        var fixture = Create(new FakeSecretStore(fails: true));
        var handler = fixture.Handler;
        var profiles = fixture.Profiles;

        var outcome = await handler.RunAsync(
            new AddProfileEffect(_work, new SecretText("hunter2-secret"), ProfileCatalog.Empty.With(_work)),
            CancellationToken.None);

        var failed = Assert.IsType<ProfileOperationFailed>(outcome);

        Assert.Contains("keyring locked", failed.Message);
        Assert.Empty(profiles.Store.Load().Catalog.Profiles);
    }

    [Fact]
    public async Task GivenStoredProfile_WhenRemovingWithToken_ThenTokenAndProfileAreGone()
    {
        var store = new FakeSecretStore();
        var fixture = Create(store);
        var handler = fixture.Handler;
        var profiles = fixture.Profiles;
        var both = ProfileCatalog.Empty.With(_work).With(_dev);

        await handler.RunAsync(new AddProfileEffect(_dev, new SecretText("hunter2-secret"), both), CancellationToken.None);
        profiles.BoardKinds.Save(_dev, 7, BoardKind.Scrum);
        await handler.RunAsync(
            new RemoveProfileEffect(_dev, DeleteToken: true, both.Without("dev"), WasActive: false),
            CancellationToken.None);

        Assert.Empty(store.Secrets);
        Assert.Empty(profiles.BoardKinds.Load(_dev));
        Assert.Equal(["work"], profiles.Store.Load().Catalog.Profiles.Select(profile => profile.Name));
    }

    [Fact]
    public async Task GivenSharedToken_WhenRemovingWithoutTokenDeletion_ThenTokenStays()
    {
        var store = new FakeSecretStore();
        var handler = Create(store).Handler;
        var catalog = ProfileCatalog.Empty.With(_work);

        await handler.RunAsync(new AddProfileEffect(_work, new SecretText("hunter2-secret"), catalog), CancellationToken.None);
        await handler.RunAsync(
            new RemoveProfileEffect(_work with { Name = "twin" }, DeleteToken: false, catalog, WasActive: false),
            CancellationToken.None);

        Assert.Single(store.Secrets);
    }

    [Fact]
    public async Task GivenSaveEffect_WhenRunning_ThenActiveProfileIsPersisted()
    {
        var fixture = Create(new FakeSecretStore());
        var handler = fixture.Handler;
        var profiles = fixture.Profiles;
        var catalog = ProfileCatalog.Empty.With(_work).With(_dev).Activate("dev");

        var outcome = await handler.RunAsync(new SaveProfilesEffect(catalog, "Switched."), CancellationToken.None);

        Assert.Equal("Switched.", Assert.IsType<ProfilesChanged>(outcome).Message);
        Assert.Equal("dev", profiles.Store.Load().Catalog.ActiveName);
    }

    [Fact]
    public async Task GivenUnwritableConfigurationPath_WhenSaving_ThenFailsWithAMessage()
    {
        Directory.CreateDirectory(_directory);

        var blocked = Path.Combine(_directory, "file");

        await File.WriteAllTextAsync(blocked, "x");

        var profiles = new ProfileServices(
            new ProfileStore(Path.Combine(blocked, "inner")),
            new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => null)),
            new SiteCaches(Path.Combine(_directory, "cache")),
            new HistoryStore(Path.Combine(_directory, "state")),
            new BoardKindStore(Path.Combine(_directory, "state")));
        var handler = new ProfileEffectHandler(profiles);

        var outcome = await handler.RunAsync(
            new SaveProfilesEffect(ProfileCatalog.Empty.With(_work), "x"),
            CancellationToken.None);

        Assert.IsType<ProfileOperationFailed>(outcome);
    }

    [Fact]
    public async Task GivenActiveProfileRemoved_WhenRunning_ThenMessageNamesTheNewActiveProfile()
    {
        var handler = Create(new FakeSecretStore()).Handler;
        var both = ProfileCatalog.Empty.With(_work).With(_dev);

        var outcome = await handler.RunAsync(
            new RemoveProfileEffect(_work, DeleteToken: true, both.Without("work"), WasActive: true),
            CancellationToken.None);

        Assert.Contains("dev is now the active profile", Assert.IsType<ProfilesChanged>(outcome).Message);
    }

    [Fact]
    public async Task GivenCachedViews_WhenRemovingOneProfile_ThenOnlyItsCacheFolderIsDeleted()
    {
        var fixture = Create(new FakeSecretStore());
        var caches = fixture.Profiles.Caches;
        var key = new CacheKey(ViewSourceKind.Jql, "project = A");

        caches.For(_work).Put(key, new CachedView([], null, DateTimeOffset.UnixEpoch));
        caches.For(_dev).Put(key, new CachedView([], null, DateTimeOffset.UnixEpoch));
        caches.FlushAll();
        Assert.True(Directory.Exists(caches.FolderOf(_work)));

        await fixture.Handler.RunAsync(
            new RemoveProfileEffect(_work, DeleteToken: true, ProfileCatalog.Empty.With(_dev), WasActive: false),
            CancellationToken.None);

        Assert.False(Directory.Exists(caches.FolderOf(_work)));
        Assert.True(Directory.Exists(caches.FolderOf(_dev)));
    }

    [Fact]
    public async Task GivenCancelledToken_WhenRunning_ThenThereIsNoOutcome()
    {
        var handler = Create(new FakeSecretStore(throwsCancellation: true)).Handler;

        var outcome = await handler.RunAsync(
            new AddProfileEffect(_work, new SecretText("tok"), ProfileCatalog.Empty.With(_work)),
            CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task GivenUnexpectedFailure_WhenRunning_ThenTheOutcomeIsAFailureNotAFaultedTask()
    {
        var handler = Create(new FakeSecretStore(throwsUnexpected: true)).Handler;

        var outcome = await handler.RunAsync(
            new AddProfileEffect(_work, new SecretText("tok"), ProfileCatalog.Empty.With(_work)),
            CancellationToken.None);

        Assert.IsType<ProfileOperationFailed>(outcome);
    }

    [Fact]
    public async Task GivenQuitEffect_WhenRunning_ThenIsNotAProfileEffect()
    {
        var handler = Create(new FakeSecretStore()).Handler;

        Assert.Null(await handler.RunAsync(new QuitEffect(), CancellationToken.None));
    }

    [Fact]
    public async Task GivenStoredAndEnvironmentTokens_WhenLoadingTheSources_ThenOnlyTheSourceComesBack()
    {
        var store = new FakeSecretStore();

        await store.SetAsync(_work.CredentialKey, "stored-secret", CancellationToken.None);

        var handler = Create(store, Environment("env-secret")).Handler;

        var single = await handler.RunAsync(new LoadTokenSourcesEffect([_dev]), CancellationToken.None);
        var several = await handler.RunAsync(new LoadTokenSourcesEffect([_work, _dev]), CancellationToken.None);

        var one = Assert.IsType<TokenSourcesLoaded>(single);
        var two = Assert.IsType<TokenSourcesLoaded>(several);

        Assert.Equal(TokenSourceKind.EnvironmentVariable, one.Sources[_dev.CredentialKey]);
        Assert.Equal(TokenSourceKind.SecretStore, two.Sources[_work.CredentialKey]);
        Assert.Equal(TokenSourceKind.None, two.Sources[_dev.CredentialKey]);
        Assert.DoesNotContain("secret", two.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAFailingStore_WhenLoadingTheSources_ThenTheSourceIsUnavailableNotAFailedOperation()
    {
        var handler = Create(new FakeSecretStore(failsOnGet: true)).Handler;

        var outcome = await handler.RunAsync(new LoadTokenSourcesEffect([_work]), CancellationToken.None);

        var loaded = Assert.IsType<TokenSourcesLoaded>(outcome);

        Assert.Equal(TokenSourceKind.Unavailable, loaded.Sources[_work.CredentialKey]);
    }

    private static EnvironmentReader Environment(string value)
    {
        return new EnvironmentReader(name => name == TokenProvider.EnvironmentVariable ? value : null);
    }

    private Fixture Create(FakeSecretStore store, EnvironmentReader? environment = null)
    {
        var profiles = new ProfileServices(
            new ProfileStore(_directory),
            new TokenProvider(store, environment ?? new EnvironmentReader(_ => null)),
            new SiteCaches(Path.Combine(_directory, "cache")),
            new HistoryStore(Path.Combine(_directory, "state")),
            new BoardKindStore(Path.Combine(_directory, "state")));

        return new Fixture(new ProfileEffectHandler(profiles), profiles);
    }

    private sealed record Fixture(ProfileEffectHandler Handler, ProfileServices Profiles);
}
