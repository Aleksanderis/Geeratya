using Geeratya.Platform.Processes;
using Geeratya.Platform.Secrets;

namespace Geeratya.Platform.Tests.Secrets;

[Trait("Os", "Independent")]
public class MacOsSecretStoreTests
{
    [Fact]
    public async Task GivenToken_WhenSetting_ThenTokenGoesToStandardInputNotArguments()
    {
        var runner = new FakeProcessRunner(new ProcessResult(0, string.Empty));
        var store = new MacOsSecretStore(runner);

        var saved = await store.SetAsync("site|me", "the-token", CancellationToken.None);

        Assert.True(saved);
        Assert.Equal(["-i"], runner.Arguments.Single());
        var input = runner.StandardInputs.Single()!;
        Assert.Contains("add-generic-password -U", input);
        Assert.Contains("-w \"the-token\"", input);
        Assert.Contains("-a \"site|me\"", input);
        Assert.EndsWith("\n", input);
    }

    [Fact]
    public async Task GivenQuoteAndBackslash_WhenSetting_ThenTheyAreEscaped()
    {
        var runner = new FakeProcessRunner(new ProcessResult(0, string.Empty));
        var store = new MacOsSecretStore(runner);

        await store.SetAsync("key", "a\"b\\c", CancellationToken.None);

        Assert.Contains("-w \"a\\\"b\\\\c\"", runner.StandardInputs.Single());
    }

    [Fact]
    public async Task GivenNewlineInToken_WhenSetting_ThenRefusesWithoutRunning()
    {
        var runner = new FakeProcessRunner(new ProcessResult(0, string.Empty));
        var store = new MacOsSecretStore(runner);

        await Assert.ThrowsAsync<SecretStoreException>(
            () => store.SetAsync("key", "a\nadd-generic-password", CancellationToken.None));

        Assert.Empty(runner.Arguments);
    }

    [Fact]
    public async Task GivenFailingExit_WhenSetting_ThenThrowsWithoutToken()
    {
        var store = new MacOsSecretStore(new FakeProcessRunner(new ProcessResult(1, string.Empty)));

        var exception = await Assert.ThrowsAsync<SecretStoreException>(
            () => store.SetAsync("key", "the-token", CancellationToken.None));

        Assert.DoesNotContain("the-token", exception.Message);
    }

    [Fact]
    public async Task GivenStoredToken_WhenGetting_ThenReturnsOutputWithoutTrailingNewline()
    {
        var runner = new FakeProcessRunner(new ProcessResult(0, "the-token\n"));
        var store = new MacOsSecretStore(runner);

        Assert.Equal("the-token", await store.GetAsync("key", CancellationToken.None));
        Assert.Equal(["find-generic-password", "-s", "geeratya", "-a", "key", "-w"], runner.Arguments.Single());
    }

    [Fact]
    public async Task GivenItemNotFound_WhenGetting_ThenReturnsNull()
    {
        var store = new MacOsSecretStore(new FakeProcessRunner(new ProcessResult(44, string.Empty)));

        Assert.Null(await store.GetAsync("key", CancellationToken.None));
    }

    [Fact]
    public async Task GivenItemNotFound_WhenDeleting_ThenNoError()
    {
        var store = new MacOsSecretStore(new FakeProcessRunner(new ProcessResult(44, string.Empty)));

        await store.DeleteAsync("key", CancellationToken.None);
    }

    [Fact]
    public async Task GivenOtherFailure_WhenDeleting_ThenThrows()
    {
        var store = new MacOsSecretStore(new FakeProcessRunner(new ProcessResult(1, string.Empty)));

        await Assert.ThrowsAsync<SecretStoreException>(() => store.DeleteAsync("key", CancellationToken.None));
    }

    [Fact]
    public async Task GivenSecurityMissing_WhenUsing_ThenStoreBecomesUnavailable()
    {
        var store = new MacOsSecretStore(FakeProcessRunner.Missing());

        Assert.False(await store.SetAsync("key", "the-token", CancellationToken.None));
        Assert.Null(await store.GetAsync("key", CancellationToken.None));
        Assert.False(store.IsAvailable);
    }

    [Fact]
    public async Task GivenTimeout_WhenGetting_ThenThrowsSecretStoreException()
    {
        var store = new MacOsSecretStore(FakeProcessRunner.Failing(new TimeoutException()));

        await Assert.ThrowsAsync<SecretStoreException>(() => store.GetAsync("key", CancellationToken.None));
    }
}
