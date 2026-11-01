using Geeratya.Platform.Directories;
using Geeratya.Platform.Processes;
using Geeratya.Platform.Secrets;

namespace Geeratya.Platform.Tests.Secrets;

[Trait("Os", "Independent")]
public class LinuxSecretStoreTests
{
    [Fact]
    public async Task GivenToken_WhenSetting_ThenTokenGoesToStandardInputNotArguments()
    {
        var runner = new FakeProcessRunner(new ProcessResult(0, string.Empty));
        var store = new LinuxSecretStore(runner);

        var saved = await store.SetAsync("site|me", "the-token", CancellationToken.None);

        Assert.True(saved);
        Assert.Equal("the-token", runner.StandardInputs.Single());
        Assert.DoesNotContain("the-token", runner.Arguments.Single());
        Assert.Contains("site|me", runner.Arguments.Single());
    }

    [Fact]
    public async Task GivenStoredToken_WhenGetting_ThenReturnsOutput()
    {
        var store = new LinuxSecretStore(new FakeProcessRunner(new ProcessResult(0, "the-token")));

        Assert.Equal("the-token", await store.GetAsync("key", CancellationToken.None));
    }

    [Fact]
    public async Task GivenNotFoundExit_WhenGetting_ThenReturnsNull()
    {
        var store = new LinuxSecretStore(new FakeProcessRunner(new ProcessResult(1, string.Empty)));

        Assert.Null(await store.GetAsync("key", CancellationToken.None));
    }

    [Fact]
    public async Task GivenFailingStoreExit_WhenSetting_ThenThrowsWithoutToken()
    {
        var store = new LinuxSecretStore(new FakeProcessRunner(new ProcessResult(2, string.Empty)));

        var exception = await Assert.ThrowsAsync<SecretStoreException>(
            () => store.SetAsync("key", "the-token", CancellationToken.None));

        Assert.DoesNotContain("the-token", exception.Message);
    }

    [Fact]
    public async Task GivenSecretToolMissing_WhenUsing_ThenStoreBecomesUnavailable()
    {
        var store = new LinuxSecretStore(new FakeProcessRunner(null));

        var saved = await store.SetAsync("key", "the-token", CancellationToken.None);
        var found = await store.GetAsync("key", CancellationToken.None);

        Assert.False(saved);
        Assert.Null(found);
        Assert.False(store.IsAvailable);
    }

    [Theory]
    [InlineData(OperatingSystemKind.Linux, typeof(LinuxSecretStore))]
    [InlineData(OperatingSystemKind.MacOs, typeof(UnsupportedSecretStore))]
    public void GivenOperatingSystem_WhenCreatingStore_ThenPicksItsStore(OperatingSystemKind kind, Type expected)
    {
        var store = SecretStores.Create(kind, new FakeProcessRunner(null));

        Assert.IsType(expected, store);
    }
}
