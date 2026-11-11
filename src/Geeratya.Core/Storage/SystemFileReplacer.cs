namespace Geeratya.Core.Storage;

/// <summary>The real replace: <see cref="File.Move(string, string, bool)"/> and a thread sleep.</summary>
public sealed class SystemFileReplacer : IFileReplacer
{
    public void Replace(string temporary, string target)
    {
        File.Move(temporary, target, overwrite: true);
    }

    public void Pause(TimeSpan duration)
    {
        Thread.Sleep(duration);
    }
}
