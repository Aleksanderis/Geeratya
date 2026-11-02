using System.Runtime.Versioning;
using Geeratya.Platform.Directories;

namespace Geeratya.Platform.Files;

/// <summary>
/// Creates folders and limits files so only the user can read them. Linux and macOS use the Unix mode (0700 for
/// folders, 0600 for files); Windows relies on the default ACL of <c>%LOCALAPPDATA%</c>, which already restricts the
/// tree to the user, so there is nothing to set (an explicit branch, not an omission).
/// </summary>
public sealed class PrivateFiles
{
    private const UnixFileMode PrivateFolderMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly OperatingSystemKind _operatingSystem;

    public PrivateFiles(OperatingSystemKind operatingSystem)
    {
        _operatingSystem = operatingSystem;
    }

    /// <summary>Creates <paramref name="path"/> and missing parents; on Unix every created folder is 0700.</summary>
    public void CreateDirectory(string path)
    {
        if (OperatingSystem.IsWindows() || _operatingSystem == OperatingSystemKind.Windows)
        {
            Directory.CreateDirectory(path);

            return;
        }

        CreateUnixDirectory(path);
    }

    /// <summary>Limits an existing file to the user (0600 on Unix, unchanged on Windows).</summary>
    public void RestrictFile(string path)
    {
        if (OperatingSystem.IsWindows() || _operatingSystem == OperatingSystemKind.Windows)
        {
            return;
        }

        RestrictUnixFile(path);
    }

    [UnsupportedOSPlatform("windows")]
    private static void CreateUnixDirectory(string path)
    {
        var missing = new Stack<string>();

        for (var current = Path.GetFullPath(path); !Directory.Exists(current); current = Path.GetDirectoryName(current)!)
        {
            missing.Push(current);
        }

        while (missing.TryPop(out var folder))
        {
            Directory.CreateDirectory(folder, PrivateFolderMode);
            File.SetUnixFileMode(folder, PrivateFolderMode);
        }
    }

    [UnsupportedOSPlatform("windows")]
    private static void RestrictUnixFile(string path)
    {
        File.SetUnixFileMode(path, PrivateFileMode);
    }
}
