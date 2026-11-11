namespace Geeratya.Core.Storage;

/// <summary>
/// The real replace and a thread sleep. The temporary file is moved over the target. On Windows a move over a file
/// that another handle has open fails with access denied even when that handle allows deleting
/// (<c>FileShare.Delete</c>); then <c>File.Replace</c> is used, which succeeds and leaves the reader reading the old
/// content. The move comes first because it is the lighter call and behaves well when two instances write the same
/// target at once. Other systems replace the target atomically whatever is open and only use the move.
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
            File.Replace(temporary, target, destinationBackupFileName: null);
        }
    }

    public void Pause(TimeSpan duration)
    {
        Thread.Sleep(duration);
    }
}
