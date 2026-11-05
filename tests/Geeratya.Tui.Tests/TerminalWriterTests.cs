using Geeratya.Platform.Clipboard;

namespace Geeratya.Tui.Tests;

[Trait("Os", "Independent")]
public sealed class TerminalWriterTests : IDisposable
{
    private readonly StringWriter _console = new();
    private readonly QueuedUiThread _uiThread = new();

    [Fact]
    public async Task GivenAWriteFromAnotherThread_WhenTheUiThreadHasNotRunIt_ThenNothingReachesTheConsole()
    {
        var writer = new TerminalWriter(_console, _uiThread);

        var pending = writer.WriteAsync("sequence".AsMemory(), CancellationToken.None);

        Assert.Equal(string.Empty, _console.ToString());
        Assert.False(pending.IsCompleted);

        _uiThread.RunQueued();
        await pending;

        Assert.Equal("sequence", _console.ToString());
    }

    [Fact]
    public async Task GivenTheOsc52Clipboard_WhenCopying_ThenTheSequenceIsWrittenOnlyWhenTheUiThreadRuns()
    {
        var clipboard = new Osc52Clipboard(new TerminalWriter(_console, _uiThread));

        var copying = clipboard.CopyAsync("text", CancellationToken.None);

        Assert.Equal(string.Empty, _console.ToString());

        while (!copying.IsCompleted)
        {
            _uiThread.RunQueued();
            await Task.Yield();
        }

        var result = await copying;
        Assert.True(result.Copied);
        Assert.Contains("]52;c;", _console.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenNoRunningApplication_WhenWriting_ThenItWritesInline()
    {
        var writer = new TerminalWriter(_console, new ApplicationUiThread());

        await writer.WriteAsync("now".AsMemory(), CancellationToken.None);

        Assert.Equal("now", _console.ToString());
    }

    public void Dispose()
    {
        _console.Dispose();
    }

    private sealed class QueuedUiThread : IUiThread
    {
        private readonly Queue<Item> _queue = new();

        public Task InvokeAsync(Action action, CancellationToken cancellationToken)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_queue)
            {
                _queue.Enqueue(new Item(action, done));
            }

            return done.Task;
        }

        public void RunQueued()
        {
            while (true)
            {
                Item? item;

                lock (_queue)
                {
                    if (!_queue.TryDequeue(out item))
                    {
                        return;
                    }
                }

                item.Action();
                item.Done.SetResult();
            }
        }
    }

    private sealed record Item(Action Action, TaskCompletionSource Done);
}
