using Geeratya.Core.Profiles;
using Geeratya.Platform;
using Geeratya.Platform.Directories;
using Geeratya.Platform.Processes;
using Geeratya.Platform.Secrets;

namespace Geeratya.Tui;

/// <summary>The profile file and token lookup, wired once in the composition root.</summary>
public sealed record ProfileServices(ProfileStore Store, TokenProvider Tokens)
{
    public static ProfileServices Create(
        OperatingSystemKind operatingSystem,
        EnvironmentReader environment,
        PlatformFolders folders)
    {
        var directories = new ApplicationDirectories(operatingSystem, environment, folders);
        var secretStore = SecretStores.Create(operatingSystem, new ProcessRunner());

        return new ProfileServices(
            new ProfileStore(directories.ConfigDirectory),
            new TokenProvider(secretStore, environment));
    }
}
