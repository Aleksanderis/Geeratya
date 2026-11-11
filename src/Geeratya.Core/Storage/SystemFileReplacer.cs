namespace Geeratya.Core.Storage;

/// <summary>
/// The real replace and a thread sleep. On Windows an existing target is swapped with <c>File.Replace</c>, not
/// <c>File.Move</c>: a move over a file that another handle has open fails with access denied even when that handle
/// allows deleting (<c>FileShare.Delete</c>), while the replace succeeds and the reader keeps reading the old
/// content. A target that does not exist yet, and every other system (where a rename replaces the target atomically
/// whatever is open), uses a move.
/// </summary>
public sealed class SystemFileReplacer : IFileReplacer
{
    public void Replace(string temporary, string target)
    {
        if (OperatingSystem.IsWindows() && File.Exists(target))
        {
            try
            {
                File.Replace(temporary, target, destinationBackupFileName: null);

                return;
            }
            catch (FileNotFoundException) when (File.Exists(temporary))
            {
                // The target was deleted between the check and the replace (another instance); create it instead.
            }
        }

        File.Move(temporary, target, overwrite: true);
    }

    public void Pause(TimeSpan duration)
    {
        Thread.Sleep(duration);
    }
}
