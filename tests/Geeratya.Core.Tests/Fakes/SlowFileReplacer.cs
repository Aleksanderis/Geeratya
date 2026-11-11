using Geeratya.Core.Storage;

namespace Geeratya.Core.Tests.Fakes;

/// <summary>
/// A slow disk: every replace takes <c>delay</c>. <see cref="FirstWrite"/> completes when the first one starts, so a
/// test can act while a batch is in progress.
/// </summary>
public sealed class SlowFileReplacer(TimeSpan delay) : IFileReplacer
{
    public TaskCompletionSource FirstWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Replace(string temporary, string target)
    {
        FirstWrite.TrySetResult();
        Thread.Sleep(delay);
        File.Move(temporary, target, overwrite: true);
    }

    public void Pause(TimeSpan duration)
    {
    }
}
