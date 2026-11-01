using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;
using Geeratya.Tui.Tests.Fakes;

namespace Geeratya.Tui.Tests;

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
        await handler.RunAsync(
            new RemoveProfileEffect(_dev, DeleteToken: true, both.Without("dev")),
            CancellationToken.None);

        Assert.Empty(store.Secrets);
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
            new RemoveProfileEffect(_work with { Name = "twin" }, DeleteToken: false, catalog),
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
            new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => null)));
        var handler = new ProfileEffectHandler(profiles);

        var outcome = await handler.RunAsync(
            new SaveProfilesEffect(ProfileCatalog.Empty.With(_work), "x"),
            CancellationToken.None);

        Assert.IsType<ProfileOperationFailed>(outcome);
    }

    [Fact]
    public async Task GivenQuitEffect_WhenRunning_ThenIsNotAProfileEffect()
    {
        var handler = Create(new FakeSecretStore()).Handler;

        Assert.Null(await handler.RunAsync(new QuitEffect(), CancellationToken.None));
    }

    private Fixture Create(FakeSecretStore store)
    {
        var profiles = new ProfileServices(
            new ProfileStore(_directory),
            new TokenProvider(store, new EnvironmentReader(_ => null)));

        return new Fixture(new ProfileEffectHandler(profiles), profiles);
    }

    private sealed record Fixture(ProfileEffectHandler Handler, ProfileServices Profiles);
}
