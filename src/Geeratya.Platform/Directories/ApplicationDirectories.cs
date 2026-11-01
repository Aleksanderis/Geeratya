namespace Geeratya.Platform.Directories;

/// <summary>
/// Where Geeratya keeps its configuration, state and cache. Linux and macOS follow the XDG base directories
/// (a relative XDG value is ignored, as the specification says); Windows uses the known folders.
/// </summary>
public sealed class ApplicationDirectories
{
    private const string ApplicationFolder = "geeratya";
    private const string WindowsApplicationFolder = "Geeratya";

    public ApplicationDirectories(
        OperatingSystemKind operatingSystem,
        EnvironmentReader environment,
        PlatformFolders folders)
    {
        if (operatingSystem == OperatingSystemKind.Windows)
        {
            ConfigDirectory = Path.Combine(folders.RoamingAppData, WindowsApplicationFolder);
            StateDirectory = Path.Combine(folders.LocalAppData, WindowsApplicationFolder, "State");
            CacheDirectory = Path.Combine(folders.LocalAppData, WindowsApplicationFolder, "Cache");

            return;
        }

        ConfigDirectory = XdgDirectory(environment, "XDG_CONFIG_HOME", Path.Combine(folders.Home, ".config"));
        StateDirectory = XdgDirectory(
            environment,
            "XDG_STATE_HOME",
            Path.Combine(folders.Home, ".local", "state"));
        CacheDirectory = XdgDirectory(environment, "XDG_CACHE_HOME", Path.Combine(folders.Home, ".cache"));
    }

    public string ConfigDirectory { get; }

    public string StateDirectory { get; }

    public string CacheDirectory { get; }

    /// <summary>Picks the operating system the process runs on.</summary>
    public static OperatingSystemKind DetectOperatingSystem()
    {
        if (OperatingSystem.IsWindows())
        {
            return OperatingSystemKind.Windows;
        }

        return OperatingSystem.IsMacOS() ? OperatingSystemKind.MacOs : OperatingSystemKind.Linux;
    }

    private static string XdgDirectory(EnvironmentReader environment, string variable, string fallback)
    {
        var configured = environment.Get(variable);
        var root = configured is not null && Path.IsPathRooted(configured) ? configured : fallback;

        return Path.Combine(root, ApplicationFolder);
    }
}
