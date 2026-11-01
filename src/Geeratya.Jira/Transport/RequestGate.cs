namespace Geeratya.Jira.Transport;

/// <summary>
/// Concurrency cap with two lanes. User requests may use every slot and are granted first; background requests
/// may use at most <c>capacity - reserved</c> slots (at least one), so a slot stays free for the user. Waiting is
/// cancellable.
/// </summary>
internal sealed class RequestGate
{
    private readonly object _lock = new();
    private readonly int _capacity;
    private readonly int _backgroundLimit;
    private readonly LinkedList<TaskCompletionSource> _user = new();
    private readonly LinkedList<TaskCompletionSource> _background = new();
    private int _running;

    public RequestGate(int capacity, int reservedForUser)
    {
        _capacity = Math.Max(1, capacity);
        _backgroundLimit = Math.Max(1, _capacity - Math.Max(0, reservedForUser));
    }

    public Task AcquireAsync(JiraPriority priority, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        var queue = priority == JiraPriority.User ? _user : _background;
        TaskCompletionSource waiter;
        LinkedListNode<TaskCompletionSource> node;

        lock (_lock)
        {
            if (CanRun(priority) && _user.Count == 0 && (priority == JiraPriority.User || _background.Count == 0))
            {
                _running++;

                return Task.CompletedTask;
            }

            waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            node = queue.AddLast(waiter);
        }

        if (cancellationToken.CanBeCanceled)
        {
            // A waiter that was granted a slot meanwhile is already completed, so cancelling it is a no-op.
            var registration = cancellationToken.Register(() => Cancel(queue, node, waiter, cancellationToken));
            _ = waiter.Task.ContinueWith(
                _ => registration.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        return waiter.Task;
    }

    public void Release()
    {
        lock (_lock)
        {
            _running--;
            Dispatch();
        }
    }

    private bool CanRun(JiraPriority priority) =>
        priority == JiraPriority.User ? _running < _capacity : _running < _backgroundLimit;

    private void Cancel(
        LinkedList<TaskCompletionSource> queue,
        LinkedListNode<TaskCompletionSource> node,
        TaskCompletionSource waiter,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (node.List == queue)
            {
                queue.Remove(node);
            }
        }

        waiter.TrySetCanceled(cancellationToken);
    }

    private void Dispatch()
    {
        while (_user.First is { } user && CanRun(JiraPriority.User))
        {
            _user.RemoveFirst();
            _running++;
            user.Value.TrySetResult();
        }

        while (_background.First is { } background && _user.Count == 0 && CanRun(JiraPriority.Background))
        {
            _background.RemoveFirst();
            _running++;
            background.Value.TrySetResult();
        }
    }
}
