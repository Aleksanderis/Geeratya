using Geeratya.Platform;
using Geeratya.Platform.Directories;
using Geeratya.Platform.Secrets;

namespace Geeratya.Tui.Tests;

public class ProfileServicesTests
{
    [Fact]
    public async Task GivenMacOs_WhenCreating_ThenTokenComesFromEnvironmentVariable()
    {
        var environment = new EnvironmentReader(name => name == TokenProvider.EnvironmentVariable ? "secret" : null);
        var folders = new PlatformFolders("/home/user", "/roaming", "/local");

        var services = ProfileServices.Create(OperatingSystemKind.MacOs, environment, folders);
        var resolution = await services.Tokens.ResolveAsync("key", CancellationToken.None);

        Assert.False(services.Tokens.Store.IsAvailable);
        Assert.Equal(TokenSource.EnvironmentVariable, resolution.Source);
    }
}
