using Geeratya.Core.Profiles;
using Geeratya.Platform;
using Geeratya.Platform.Directories;
using Geeratya.Platform.Processes;
using Geeratya.Platform.Secrets;

namespace Geeratya.Tui;

/// <summary>
/// The profile file and token lookup, wired once in the composition root and used by the shell for the profile commands.
/// </summary>
// ReSharper disable NotAccessedPositionalProperty.Global
public sealed record ProfileServices(ProfileStore Store, TokenProvider Tokens)
{
    /// <summary>
    /// Wires the services for the running process; throws <see cref="InvalidOperationException"/> with a readable
    /// message when no home directory can be found.
    /// </summary>
    public static ProfileServices CreateForProcess()
    {
        return Create(
            ApplicationDirectories.DetectOperatingSystem(),
            EnvironmentReader.ForProcess(),
            PlatformFolders.ForCurrentUser());
    }

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
