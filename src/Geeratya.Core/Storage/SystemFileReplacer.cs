namespace Geeratya.Core.Storage;

/// <summary>
/// The real replace and a thread sleep. The temporary file is moved over the target. On Windows a move over a file
/// that another handle has open fails with access denied even when that handle allows deleting
/// (<c>FileShare.Delete</c>); then <c>File.Replace</c> is used, which succeeds and leaves the reader reading the old
/// content. The move comes first because it is the lighter call and behaves well when two instances write the same
/// target at once, and a target that vanishes under the replace is moved over instead. Other systems replace the
/// target atomically whatever is open and only use the move.
/// </summary>
public sealed class SystemFileReplacer : IFileReplacer
{
    public void Replace(string temporary, string target)
    {
        try
        {
            File.Move(temporary, target, overwrite: true);
        }
        catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows() && File.Exists(target))
        {
            ReplaceExisting(temporary, target);
        }
    }

    public void Pause(TimeSpan duration)
    {
        Thread.Sleep(duration);
    }

    // Another instance replacing the same target at once can remove it between the check and this call, which
    // File.Replace reports as FileNotFoundException; the temporary file is still there, so a plain move finishes it.
    private static void ReplaceExisting(string temporary, string target)
    {
        try
        {
            File.Replace(temporary, target, destinationBackupFileName: null);
        }
        catch (FileNotFoundException) when (File.Exists(temporary))
        {
            File.Move(temporary, target, overwrite: true);
        }
    }
}
