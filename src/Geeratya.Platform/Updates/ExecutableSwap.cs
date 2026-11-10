using Geeratya.Platform.Directories;

namespace Geeratya.Platform.Updates;

/// <summary>
/// Puts a new binary in place of the running one. Linux and macOS rename the new file over the old one (atomic, the
/// running process keeps its open inode). Windows cannot overwrite or delete a running <c>.exe</c> but can rename
/// it, so the running binary moves aside to <c>geeratya.old.exe</c>, the new one takes its name, and the old file is
/// deleted at once when possible or by <see cref="RemoveLeftover"/> on a later start.
/// </summary>
public sealed class ExecutableSwap
{
    private readonly OperatingSystemKind _operatingSystem;

    public ExecutableSwap(OperatingSystemKind operatingSystem)
    {
        _operatingSystem = operatingSystem;
    }

    /// <summary>
    /// Where the new binary is written first, next to <paramref name="executable"/> (same file system).
    /// </summary>
    public static string StagedPath(string executable)
    {
        return Sibling(executable, "new");
    }

    /// <summary>Where a replaced binary waits on Windows.</summary>
    public static string OldPath(string executable)
    {
        return Sibling(executable, "old");
    }

    /// <summary>
    /// Replaces <paramref name="executable"/> with the file at <paramref name="staged"/>. Throws
    /// <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> when it cannot; the old binary is then
    /// still the one at <paramref name="executable"/>.
    /// </summary>
    public void Replace(string executable, string staged)
    {
        if (_operatingSystem == OperatingSystemKind.Windows)
        {
            ReplaceByRenaming(executable, staged);

            return;
        }

        MakeExecutable(staged);
        File.Move(staged, executable, true);
    }

    /// <summary>
    /// Deletes a binary an earlier Windows update moved aside; quiet when there is none or it is in use.
    /// </summary>
    public void RemoveLeftover(string executable)
    {
        // Only a Windows update leaves a file behind; elsewhere a file of that name is none of our business.
        if (_operatingSystem == OperatingSystemKind.Windows)
        {
            TryDelete(OldPath(executable));
        }
    }

    /// <summary>Gives a staged binary the execute permission (Linux and macOS; Windows needs none).</summary>
    public void MakeExecutable(string path)
    {
        if (_operatingSystem != OperatingSystemKind.Windows && !OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    private static string Sibling(string executable, string tag)
    {
        var folder = Path.GetDirectoryName(executable) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(executable);
        var extension = Path.GetExtension(executable);

        return Path.Combine(folder, $"{name}.{tag}{extension}");
    }

    private static void ReplaceByRenaming(string executable, string staged)
    {
        var old = OldPath(executable);

        // A leftover from an update before this one: gone when no process runs it any more.
        File.Delete(old);
        File.Move(executable, old);

        try
        {
            File.Move(staged, executable);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            File.Move(old, executable);

            throw;
        }

        TryDelete(old);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Still running or locked: the next start tries again.
        }
    }
}
