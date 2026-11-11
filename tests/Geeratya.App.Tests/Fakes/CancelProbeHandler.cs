namespace Geeratya.App.Tests.Fakes;

/// <summary>
/// Holds every request until it is cancelled. A callback registered on the request's token records the thread that
/// runs it and then blocks until <see cref="Release"/>, so a test can tell whether cancelling (or disposing the
/// transport) ran foreign code on the thread that asked for it, and whether that thread was held up.
/// </summary>
public sealed class CancelProbeHandler : HttpMessageHandler
{
    private static readonly TimeSpan _blockLimit = TimeSpan.FromSeconds(10);

    private readonly TaskCompletionSource _requestArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<int> _callbackThread = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ManualResetEventSlim _release = new();

    public Task RequestArrived => _requestArrived.Task;

    /// <summary>The managed thread id the token callback ran on; completes when the callback starts.</summary>
    public Task<int> CallbackThread => _callbackThread.Task;

    public void Release()
    {
        _release.Set();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Not disposed: disposing waits for a running callback and would skip one that has not started yet.
        _ = cancellationToken.Register(RecordAndBlock);

        _requestArrived.TrySetResult();

        await new TaskCompletionSource().Task.WaitAsync(cancellationToken);

        throw new InvalidOperationException("Unreachable: the wait only ends by cancellation.");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _release.Dispose();
        }

        base.Dispose(disposing);
    }

    private void RecordAndBlock()
    {
        _callbackThread.TrySetResult(Environment.CurrentManagedThreadId);
        _release.Wait(_blockLimit);
    }
}
