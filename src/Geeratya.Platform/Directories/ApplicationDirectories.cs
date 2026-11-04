namespace Geeratya.Platform.Directories;

/// <summary>
/// Where Geeratya keeps its configuration, state and cache. Linux and macOS follow the XDG base directories
/// (a relative XDG value is ignored, as the specification says); Windows uses the known folders. Only with
/// <c>GEERATYA_USE_XDG=1</c> does Windows honour a fully qualified XDG value too, which scripts and tests use to keep
/// the real profile, state and cache out of a run (.NET reads the known folders from the shell, so changing
/// <c>APPDATA</c> does not redirect them); a global <c>XDG_CONFIG_HOME</c> alone never moves a Windows install.
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
            var useXdg = environment.Get("GEERATYA_USE_XDG") == "1";

            ConfigDirectory = (useXdg ? ConfiguredXdg(environment, "XDG_CONFIG_HOME", Path.IsPathFullyQualified) : null)
                ?? Path.Combine(folders.RoamingAppData, WindowsApplicationFolder);
            StateDirectory = (useXdg ? ConfiguredXdg(environment, "XDG_STATE_HOME", Path.IsPathFullyQualified) : null)
                ?? Path.Combine(folders.LocalAppData, WindowsApplicationFolder, "State");
            CacheDirectory = (useXdg ? ConfiguredXdg(environment, "XDG_CACHE_HOME", Path.IsPathFullyQualified) : null)
                ?? Path.Combine(folders.LocalAppData, WindowsApplicationFolder, "Cache");

            return;
        }

        ConfigDirectory = XdgDirectory(environment, "XDG_CONFIG_HOME", folders.Home, ".config");
        StateDirectory = XdgDirectory(environment, "XDG_STATE_HOME", folders.Home, ".local", "state");
        CacheDirectory = XdgDirectory(environment, "XDG_CACHE_HOME", folders.Home, ".cache");
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

    private static string? ConfiguredXdg(EnvironmentReader environment, string variable, Func<string, bool> isAbsolute)
    {
        var configured = environment.Get(variable);

        return configured is not null && isAbsolute(configured)
            ? Path.Combine(configured, ApplicationFolder)
            : null;
    }

    private static string XdgDirectory(
        EnvironmentReader environment,
        string variable,
        string home,
        params string[] fallbackSegments)
    {
        var configured = ConfiguredXdg(environment, variable, Path.IsPathRooted);

        if (configured is not null)
        {
            return configured;
        }

        if (!Path.IsPathRooted(home))
        {
            throw new InvalidOperationException(
                $"Cannot locate the user's home directory (HOME is unset or not an absolute path), and {variable} is unset or not an absolute path. Set HOME or {variable}.");
        }

        return Path.Combine([home, .. fallbackSegments, ApplicationFolder]);
    }
}
