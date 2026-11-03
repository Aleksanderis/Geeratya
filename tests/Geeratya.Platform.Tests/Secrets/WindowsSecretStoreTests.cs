using Geeratya.Platform.Secrets;

namespace Geeratya.Platform.Tests.Secrets;

public class WindowsSecretStoreTests
{
    [Fact]
    public async Task GivenAToken_WhenSettingGettingAndDeleting_ThenItRoundTripsAndIsRemoved()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var store = new WindowsSecretStore();
        var key = $"test|{Guid.NewGuid():N}";

        try
        {
            Assert.True(await store.SetAsync(key, "first-value", CancellationToken.None));
            Assert.Equal("first-value", await store.GetAsync(key, CancellationToken.None));

            await store.SetAsync(key, "second-value", CancellationToken.None);
            Assert.Equal("second-value", await store.GetAsync(key, CancellationToken.None));
        }
        finally
        {
            await store.DeleteAsync(key, CancellationToken.None);
        }

        Assert.Null(await store.GetAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task GivenAMissingKey_WhenDeleting_ThenItDoesNotThrow()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var store = new WindowsSecretStore();

        await store.DeleteAsync($"test|{Guid.NewGuid():N}", CancellationToken.None);
    }
}
