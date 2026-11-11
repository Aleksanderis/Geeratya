using Geeratya.Core.Storage;

namespace Geeratya.Core.Tests.Fakes;

/// <summary>
/// A slow disk: every replace takes <c>delay</c> and tells the test which target it is writing, so a test can act
/// while a batch is in progress.
/// </summary>
public sealed class SlowFileReplacer(TimeSpan delay, Action<string> onReplace) : IFileReplacer
{
    public void Replace(string temporary, string target)
    {
        onReplace(target);
        Thread.Sleep(delay);
        File.Move(temporary, target, overwrite: true);
    }

    public void Pause(TimeSpan duration)
    {
    }
}
