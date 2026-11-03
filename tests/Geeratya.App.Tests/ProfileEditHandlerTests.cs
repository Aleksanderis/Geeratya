using System.Net;
using Geeratya.App.Tests.Fakes;
using Geeratya.Core.BoardKinds;
using Geeratya.Core.Cache;
using Geeratya.Core.History;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;

namespace Geeratya.App.Tests;

public sealed class ProfileEditHandlerTests : IDisposable
{
    private const string Token = "tok-s3cret-value";

    private const string Myself =
        """{"accountId":"abc","displayName":"Test User 1","emailAddress":"me@a.test","active":true}""";

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
    public async Task GivenAStoredToken_WhenEditingTheUrlWithoutAToken_ThenTheTokenMovesAndRemovingDeletesIt()
    {
        var store = new FakeSecretStore();
        var fixture = Create(store);
        var catalog = ProfileCatalog.Empty.With(_work);

        await fixture.Handler.RunAsync(
            new AddProfileEffect(_work, new SecretText(Token), catalog),
            CancellationToken.None);

        var moved = new Profile("work", "https://new.atlassian.net", "me@work.test");
        var changed = ProfileCatalog.Empty.With(moved);
        var update = new UpdateProfileEffect(_work, moved, null, changed, OldKeyShared: false);

        var outcome = await fixture.Handler.RunAsync(update, CancellationToken.None);

        var reported = Assert.IsType<ProfilesChanged>(outcome);

        Assert.Equal(Token, store.Secrets[moved.CredentialKey]);
        Assert.DoesNotContain(_work.CredentialKey, store.Secrets.Keys);
        Assert.Equal("https://new.atlassian.net", fixture.Profiles.Store.Load().Catalog.Active!.SiteUrl);
        Assert.DoesNotContain(Token, reported.Message);
        Assert.False(reported.Reconnect);

        await fixture.Handler.RunAsync(
            new RemoveProfileEffect(moved, DeleteToken: true, ProfileCatalog.Empty, WasActive: true),
            CancellationToken.None);

        Assert.Empty(store.Secrets);
        Assert.Empty(fixture.Profiles.Store.Load().Catalog.Profiles);
    }

    [Fact]
    public async Task GivenAKeptTokenAndTheSameKey_WhenRenaming_ThenTheStoreIsUntouchedAndNoReconnectIsAsked()
    {
        var store = new FakeSecretStore();
        var fixture = Create(store);

        await fixture.Handler.RunAsync(
            new AddProfileEffect(_work, new SecretText(Token), ProfileCatalog.Empty.With(_work)),
            CancellationToken.None);

        var renamed = new Profile("office", _work.SiteUrl, _work.Email);
        var update = new UpdateProfileEffect(_work, renamed, null, ProfileCatalog.Empty.With(renamed), false);

        var outcome = Assert.IsType<ProfilesChanged>(await fixture.Handler.RunAsync(update, CancellationToken.None));

        Assert.Equal(Token, store.Secrets[_work.CredentialKey]);
        Assert.False(outcome.Reconnect);
        Assert.Equal("office", fixture.Profiles.Store.Load().Catalog.ActiveName);
    }

    [Fact]
    public async Task GivenATypedToken_WhenEditingTheActiveProfile_ThenItReplacesTheStoredOneAndAsksToReconnect()
    {
        var store = new FakeSecretStore();
        var fixture = Create(store);

        await fixture.Handler.RunAsync(
            new AddProfileEffect(_work, new SecretText("old-token"), ProfileCatalog.Empty.With(_work)),
            CancellationToken.None);

        var catalog = ProfileCatalog.Empty.With(_work);
        var update = new UpdateProfileEffect(_work, _work, new SecretText(Token), catalog, false);

        var outcome = Assert.IsType<ProfilesChanged>(await fixture.Handler.RunAsync(update, CancellationToken.None));

        Assert.Equal(Token, store.Secrets[_work.CredentialKey]);
        Assert.True(outcome.Reconnect);
        Assert.DoesNotContain(Token, outcome.Message);
    }

    [Fact]
    public async Task GivenNoStoredToken_WhenTheUrlChangesWithoutATypedToken_ThenNothingIsSavedAndTheMessageSaysSo()
    {
        var fixture = Create(new FakeSecretStore());
        var moved = new Profile("work", "https://new.atlassian.net", "me@work.test");
        var update = new UpdateProfileEffect(_work, moved, null, ProfileCatalog.Empty.With(moved), false);

        var outcome = await fixture.Handler.RunAsync(update, CancellationToken.None);

        var failed = Assert.IsType<ProfileOperationFailed>(outcome);

        Assert.Contains("type the token again", failed.Message);
        Assert.Empty(fixture.Profiles.Store.Load().Catalog.Profiles);
    }

    [Fact]
    public async Task GivenTheOldKeyIsSharedByAnotherProfile_WhenTheUrlChanges_ThenTheOldTokenAndDataStay()
    {
        var store = new FakeSecretStore();
        var fixture = Create(store);
        var twin = new Profile("twin", _work.SiteUrl, _work.Email);
        var both = ProfileCatalog.Empty.With(_work).With(twin);

        await fixture.Handler.RunAsync(
            new AddProfileEffect(_work, new SecretText(Token), both),
            CancellationToken.None);
        fixture.Profiles.BoardKinds.Save(_work, 7, BoardKind.Scrum);

        var moved = new Profile("work", "https://new.atlassian.net", _work.Email);
        var update = new UpdateProfileEffect(_work, moved, null, both.Replace("work", moved), OldKeyShared: true);

        await fixture.Handler.RunAsync(update, CancellationToken.None);

        Assert.Equal(Token, store.Secrets[_work.CredentialKey]);
        Assert.Equal(Token, store.Secrets[moved.CredentialKey]);
        Assert.NotEmpty(fixture.Profiles.BoardKinds.Load(_work));
    }

    [Fact]
    public async Task GivenTheOldKeyIsFree_WhenTheUrlChanges_ThenTheOldLocalDataIsDeleted()
    {
        var store = new FakeSecretStore();
        var fixture = Create(store);

        await fixture.Handler.RunAsync(
            new AddProfileEffect(_work, new SecretText(Token), ProfileCatalog.Empty.With(_work)),
            CancellationToken.None);
        fixture.Profiles.BoardKinds.Save(_work, 7, BoardKind.Scrum);

        var moved = new Profile("work", "https://new.atlassian.net", _work.Email);

        await fixture.Handler.RunAsync(
            new UpdateProfileEffect(_work, moved, null, ProfileCatalog.Empty.With(moved), false),
            CancellationToken.None);

        Assert.Empty(fixture.Profiles.BoardKinds.Load(_work));
    }

    [Fact]
    public async Task GivenTheOldTokenCannotBeDeleted_WhenTheUrlChanges_ThenTheEditIsStillReportedAsSaved()
    {
        var store = new FakeSecretStore();
        var fixture = Create(store);

        await fixture.Handler.RunAsync(
            new AddProfileEffect(_work, new SecretText(Token), ProfileCatalog.Empty.With(_work)),
            CancellationToken.None);

        var failing = new FakeSecretStore(failsOnDelete: true);
        var other = Create(failing);

        await failing.SetAsync(_work.CredentialKey, Token, CancellationToken.None);

        var moved = new Profile("work", "https://new.atlassian.net", _work.Email);
        var update = new UpdateProfileEffect(_work, moved, null, ProfileCatalog.Empty.With(moved), false);

        var outcome = await other.Handler.RunAsync(update, CancellationToken.None);

        var changed = Assert.IsType<ProfilesChanged>(outcome);

        Assert.Contains("old token could not be deleted", changed.Message);
        Assert.Equal("https://new.atlassian.net", other.Profiles.Store.Load().Catalog.Active!.SiteUrl);
    }

    [Fact]
    public async Task GivenAFailingStore_WhenEditingWithANewToken_ThenTheCatalogIsNotSaved()
    {
        var fixture = Create(new FakeSecretStore(fails: true));
        var catalog = ProfileCatalog.Empty.With(_work);
        var update = new UpdateProfileEffect(_work, _work, new SecretText(Token), catalog, false);

        var outcome = await fixture.Handler.RunAsync(update, CancellationToken.None);

        var failed = Assert.IsType<ProfileOperationFailed>(outcome);

        Assert.DoesNotContain(Token, failed.Message);
        Assert.Empty(fixture.Profiles.Store.Load().Catalog.Profiles);
    }

    [Fact]
    public async Task GivenAcceptedCredentials_WhenTestingATypedToken_ThenItConnectsWithoutStoringAnything()
    {
        var store = new FakeSecretStore();
        var http = new ScriptedJiraHandler().Reply(HttpStatusCode.OK, Myself);
        var fixture = Create(store, http);

        var outcome = await fixture.Handler.RunAsync(
            new TestProfileEffect(_work, new SecretText(Token), null, false),
            CancellationToken.None);

        var tested = Assert.IsType<ProfileTested>(outcome);

        Assert.True(tested.Succeeded);
        Assert.Equal("Connected as Test User 1.", tested.Message);
        Assert.Empty(store.Secrets);
        var request = Assert.Single(http.Requests);

        Assert.Equal("work.atlassian.net", request.Host);
        Assert.EndsWith("/myself", request.AbsolutePath);
        Assert.Empty(fixture.Profiles.Store.Load().Catalog.Profiles);
    }

    [Fact]
    public async Task GivenAStoredToken_WhenTestingWithoutATypedOne_ThenTheStoredTokenIsUsed()
    {
        var store = new FakeSecretStore();
        var http = new ScriptedJiraHandler().Reply(HttpStatusCode.OK, Myself);
        var fixture = Create(store, http);

        await fixture.Handler.RunAsync(
            new AddProfileEffect(_work, new SecretText(Token), ProfileCatalog.Empty.With(_work)),
            CancellationToken.None);

        var outcome = await fixture.Handler.RunAsync(
            new TestProfileEffect(_work, null, _work.CredentialKey, false),
            CancellationToken.None);

        Assert.True(Assert.IsType<ProfileTested>(outcome).Succeeded);
        Assert.Equal("work.atlassian.net", Assert.Single(http.Requests).Host);
    }

    [Fact]
    public async Task GivenARejectedToken_WhenTesting_ThenTheMessageSaysFailedWithoutTheToken()
    {
        var http = new ScriptedJiraHandler().Reply(HttpStatusCode.Unauthorized, "Client must be authenticated.");
        var fixture = Create(new FakeSecretStore(), http);

        var outcome = await fixture.Handler.RunAsync(
            new TestProfileEffect(_dev, new SecretText(Token), null, false),
            CancellationToken.None);

        var tested = Assert.IsType<ProfileTested>(outcome);

        Assert.False(tested.Succeeded);
        Assert.StartsWith("Failed:", tested.Message);
        Assert.Contains("401", tested.Message);
        Assert.DoesNotContain(Token, tested.Message);
    }

    [Fact]
    public async Task GivenNothingStored_WhenTestingWithoutATypedToken_ThenItFailsWithoutAnyRequest()
    {
        var http = new ScriptedJiraHandler().Reply(HttpStatusCode.OK, Myself);
        var fixture = Create(new FakeSecretStore(), http);

        var outcome = await fixture.Handler.RunAsync(
            new TestProfileEffect(_work, null, _work.CredentialKey, false),
            CancellationToken.None);

        Assert.False(Assert.IsType<ProfileTested>(outcome).Succeeded);
        Assert.Empty(http.Requests);
    }

    private Fixture Create(FakeSecretStore store, ScriptedJiraHandler? http = null)
    {
        var profiles = new ProfileServices(
            new ProfileStore(_directory),
            new TokenProvider(store, new EnvironmentReader(_ => null)),
            new SiteCaches(Path.Combine(_directory, "cache")),
            new HistoryStore(Path.Combine(_directory, "state")),
            new BoardKindStore(Path.Combine(_directory, "state")));

        var transports = new FakeTransportFactory(http ?? new ScriptedJiraHandler());

        return new Fixture(new ProfileEffectHandler(profiles, transports), profiles);
    }

    private sealed record Fixture(ProfileEffectHandler Handler, ProfileServices Profiles);
}
