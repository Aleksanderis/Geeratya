using Geeratya.Core.Storage;

namespace Geeratya.Core.Tests.Fakes;

/// <summary>
/// A slow disk: every replace takes <c>delay</c>. <see cref="FirstWrite"/> is set when the first one starts, so a
/// test can act while a batch is in progress. An event, not a task, so the test waits on its own thread and never on a
/// thread-pool continuation that a busy runner may start late.
/// </summary>
public sealed class SlowFileReplacer(TimeSpan delay) : IFileReplacer, IDisposable
{
    public ManualResetEventSlim FirstWrite { get; } = new();

    public void Dispose()
    {
        FirstWrite.Dispose();
    }

    public void Replace(string temporary, string target)
    {
        FirstWrite.Set();
        Thread.Sleep(delay);
        File.Move(temporary, target, overwrite: true);
    }

    public void Pause(TimeSpan duration)
    {
    }
}
