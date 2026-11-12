using Geeratya.Platform.Directories;

namespace Geeratya.Platform.Updates;

/// <summary>
/// Tells how this binary was installed, from a marker file the install scripts write next to it, falling back to
/// where it sits. Pure: the caller reads the marker, <c>/etc/os-release</c> and the folders.
/// </summary>
public static class InstallRouteDetector
{
    /// <summary>The file the install scripts write next to the binary; its text is the word <c>script</c>.</summary>
    public const string MarkerFileName = "geeratya.install-source";

    /// <summary>The marker text of a script install.</summary>
    public const string ScriptMarker = "script";

    /// <summary>The page of the latest release, for the manual route and for failures.</summary>
    public const string ReleasesPage = "https://github.com/Aleksanderis/Geeratya/releases/latest";

    private const string ManualText = $"download the new archive from {ReleasesPage}";

    private const string BrewText = "run `brew upgrade geeratya`";

    private const string WingetText = "run `winget upgrade Geeratya.Geeratya`";

    /// <summary>The route for <paramref name="install"/>.</summary>
    public static InstallRoute Detect(InstallFacts install)
    {
        if (install.Marker?.Trim() == ScriptMarker)
        {
            return Script();
        }

        var path = Normalize(install.ExecutablePath);

        return install.OperatingSystem switch
        {
            OperatingSystemKind.Windows => DetectWindows(path, install),
            OperatingSystemKind.MacOs => DetectMac(path, install),
            _ => DetectLinux(path, install),
        };
    }

    private static InstallRoute DetectWindows(string path, InstallFacts install)
    {
        if (path.Contains("/winget/", StringComparison.OrdinalIgnoreCase))
        {
            return new InstallRoute(InstallRouteKind.PackageManager, WingetText);
        }

        return IsUnder(path, Path.Combine(install.LocalAppData, "Geeratya"), StringComparison.OrdinalIgnoreCase)
            ? Script()
            : Manual();
    }

    private static InstallRoute DetectMac(string path, InstallFacts install)
    {
        if (IsHomebrew(path))
        {
            return new InstallRoute(InstallRouteKind.PackageManager, BrewText);
        }

        return IsScriptFolder(path, install) ? Script() : Manual();
    }

    private static InstallRoute DetectLinux(string path, InstallFacts install)
    {
        if (IsHomebrew(path))
        {
            return new InstallRoute(InstallRouteKind.PackageManager, BrewText);
        }

        if (path.StartsWith("/usr/bin/", StringComparison.Ordinal)
            || path.StartsWith("/bin/", StringComparison.Ordinal))
        {
            return new InstallRoute(InstallRouteKind.PackageManager, PackageText(install.OsRelease));
        }

        return IsScriptFolder(path, install) ? Script() : Manual();
    }

    private static string PackageText(string? osRelease)
    {
        var families = OsReleaseFamilies(osRelease);
        var download = $"download the new package from {ReleasesPage} and";

        if (families.Contains("arch"))
        {
            return $"{download} run `sudo pacman -U <file>.pkg.tar.zst` (or update `geeratya-bin` with an AUR helper)";
        }

        if (families.Contains("debian") || families.Contains("ubuntu"))
        {
            return $"{download} run `sudo apt install ./<file>.deb`";
        }

        if (families.Contains("fedora") || families.Contains("rhel") || families.Contains("suse"))
        {
            return $"{download} run `sudo dnf install ./<file>.rpm` (zypper: `sudo zypper install ./<file>.rpm`)";
        }

        return $"{download} install it with your package manager";
    }

    private static HashSet<string> OsReleaseFamilies(string? osRelease)
    {
        var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in (osRelease ?? string.Empty).Split('\n'))
        {
            var separator = line.IndexOf('=', StringComparison.Ordinal);

            if (separator <= 0 || line[..separator].Trim() is not ("ID" or "ID_LIKE"))
            {
                continue;
            }

            var words = line[(separator + 1)..].Trim().Trim('"').Split(' ', StringSplitOptions.RemoveEmptyEntries);

            families.UnionWith(words);
        }

        return families;
    }

    private static bool IsHomebrew(string path)
    {
        return path.Contains("/Cellar/", StringComparison.Ordinal)
            || path.StartsWith("/opt/homebrew/", StringComparison.Ordinal)
            || path.StartsWith("/home/linuxbrew/", StringComparison.Ordinal);
    }

    private static bool IsScriptFolder(string path, InstallFacts install)
    {
        var folder = Path.Combine(install.Home, ".local", "bin");

        return IsUnder(path, folder, StringComparison.Ordinal);
    }

    private static bool IsUnder(string path, string folder, StringComparison comparison)
    {
        var prefix = $"{Normalize(folder).TrimEnd('/')}/";

        return path.StartsWith(prefix, comparison);
    }

    // One separator whatever the system, so the rules read the same everywhere (and test on any host).
    private static string Normalize(string? path)
    {
        return (path ?? string.Empty).Replace('\\', '/');
    }

    private static InstallRoute Script()
    {
        return new InstallRoute(InstallRouteKind.Script, InstallRoute.UpdateCommandText);
    }

    private static InstallRoute Manual()
    {
        return new InstallRoute(InstallRouteKind.Manual, ManualText);
    }
}
