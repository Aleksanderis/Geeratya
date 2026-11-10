using Geeratya.Platform.Directories;
using Geeratya.Platform.Updates;

namespace Geeratya.App.Updates;

/// <summary>
/// The running binary: where it is and how it got there. The route reads a marker file and
/// <c>/etc/os-release</c>, so it is worked out on first use and never on the start path.
/// </summary>
public sealed class InstalledBinary
{
    private const string OsReleasePath = "/etc/os-release";

    private readonly OperatingSystemKind _operatingSystem;
    private readonly PlatformFolders _folders;
    private InstallRoute? _route;

    public InstalledBinary(OperatingSystemKind operatingSystem, string? path, PlatformFolders folders)
    {
        _operatingSystem = operatingSystem;
        Path = path ?? string.Empty;
        _folders = folders;
    }

    /// <summary>The binary of the running process (empty when the system does not say).</summary>
    public static InstalledBinary ForProcess()
    {
        return new InstalledBinary(
            ApplicationDirectories.DetectOperatingSystem(),
            Environment.ProcessPath,
            PlatformFolders.ForCurrentUser());
    }

    public string Path { get; }

    public OperatingSystemKind OperatingSystem => _operatingSystem;

    /// <summary>How an update reaches this install.</summary>
    public InstallRoute Route => _route ??= InstallRouteDetector.Detect(ReadFacts());

    /// <summary>Removes a binary that an earlier Windows update moved aside.</summary>
    public void RemoveLeftover()
    {
        if (Path.Length > 0)
        {
            new ExecutableSwap(_operatingSystem).RemoveLeftover(Path);
        }
    }

    private InstallFacts ReadFacts()
    {
        var folder = System.IO.Path.GetDirectoryName(Path) ?? string.Empty;
        var marker = ReadQuietly(System.IO.Path.Combine(folder, InstallRouteDetector.MarkerFileName));
        var osRelease = _operatingSystem == OperatingSystemKind.Linux ? ReadQuietly(OsReleasePath) : null;

        return new InstallFacts(_operatingSystem, Path, marker, osRelease, _folders.Home, _folders.LocalAppData);
    }

    private static string? ReadQuietly(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
