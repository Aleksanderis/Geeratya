using Geeratya.Core.Cache;
using Geeratya.Core.Profiles;
using Geeratya.Core.Settings;
using Geeratya.Platform;
using Geeratya.Platform.Directories;
using Geeratya.Platform.Files;
using Geeratya.Platform.Processes;
using Geeratya.Platform.Secrets;

namespace Geeratya.App;

/// <summary>
/// The profile file, token lookup and per-profile caches, wired once in the composition root and used by the shell.
/// </summary>
// ReSharper disable NotAccessedPositionalProperty.Global
public sealed record ProfileServices(ProfileStore Store, TokenProvider Tokens,
    SiteCaches Caches,
    SettingsStore? Settings = null)
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

        var files = new PrivateFiles(operatingSystem);
        var permissions = new FilePermissions(files.CreateDirectory, files.RestrictFile);

        return new ProfileServices(
            new ProfileStore(directories.ConfigDirectory),
            new TokenProvider(secretStore, environment),
            new SiteCaches(directories.CacheDirectory, permissions),
            new SettingsStore(directories.ConfigDirectory));
    }
}
