using Geeratya.Platform.Secrets;

namespace Geeratya.Platform.Tests.Secrets;

[Trait("Os", "Independent")]
public class TokenProviderTests
{
    [Fact]
    public async Task GivenStoredToken_WhenResolving_ThenStoreWinsOverEnvironment()
    {
        var store = new FakeSecretStore();
        await store.SetAsync("key", "from-store", CancellationToken.None);
        var provider = new TokenProvider(store, new EnvironmentReader(_ => "from-environment"));

        var resolution = await provider.ResolveAsync("key", true, CancellationToken.None);

        Assert.Equal("from-store", resolution.Token);
        Assert.Equal(TokenSource.SecretStore, resolution.Source);
    }

    [Fact]
    public async Task GivenNothingStored_WhenResolving_ThenFallsBackToEnvironmentVariable()
    {
        var provider = new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => "from-environment"));

        var resolution = await provider.ResolveAsync("key", true, CancellationToken.None);

        Assert.Equal("from-environment", resolution.Token);
        Assert.Equal(TokenSource.EnvironmentVariable, resolution.Source);
    }

    [Fact]
    public async Task GivenEnvironmentNotAllowed_WhenNothingStored_ThenEnvironmentTokenIsIgnored()
    {
        var provider = new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => "from-environment"));

        var resolution = await provider.ResolveAsync("key", false, CancellationToken.None);

        Assert.Null(resolution.Token);
        Assert.Equal(TokenSource.None, resolution.Source);
    }

    [Fact]
    public async Task GivenEnvironmentNotAllowed_WhenTokenStored_ThenStoredTokenIsUsed()
    {
        var store = new FakeSecretStore();
        await store.SetAsync("key", "from-store", CancellationToken.None);
        var provider = new TokenProvider(store, new EnvironmentReader(_ => "from-environment"));

        var resolution = await provider.ResolveAsync("key", false, CancellationToken.None);

        Assert.Equal("from-store", resolution.Token);
    }

    [Fact]
    public async Task GivenUnsupportedStoreAndNoVariable_WhenResolving_ThenNoToken()
    {
        var provider = new TokenProvider(new UnsupportedSecretStore(), new EnvironmentReader(_ => null));

        var resolution = await provider.ResolveAsync("key", true, CancellationToken.None);

        Assert.Null(resolution.Token);
        Assert.Equal(TokenSource.None, resolution.Source);
    }

    [Fact]
    public void GivenToken_WhenFormattingResolution_ThenTokenIsNotShown()
    {
        var resolution = new TokenResolution("super-secret", TokenSource.SecretStore);

        Assert.DoesNotContain("super-secret", resolution.ToString());
    }
}
