using System.Diagnostics;

namespace Geeratya.Jira.Transport;

/// <summary>
/// Concurrency cap with two lanes. User requests may use every slot and are granted first; background requests
/// may use at most <c>capacity - reserved</c> slots (at least one), so a slot stays free for the user. Waiting is
/// cancellable. Every grant is a <see cref="RequestLease"/>, so a background request that holds a slot can be
/// abandoned when a user request has waited too long for one.
/// </summary>
internal sealed class RequestGate
{
    private readonly object _lock = new();
    private readonly int _capacity;
    private readonly int _backgroundLimit;
    private readonly LinkedList<TaskCompletionSource<RequestLease>> _user = new();
    private readonly LinkedList<TaskCompletionSource<RequestLease>> _background = new();
    private readonly List<RequestLease> _leases = [];

    public RequestGate(int capacity, int reservedForUser)
    {
        _capacity = Math.Max(1, capacity);
        _backgroundLimit = Math.Max(1, _capacity - Math.Max(0, reservedForUser));
    }

    public Task<RequestLease> AcquireAsync(JiraPriority priority, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<RequestLease>(cancellationToken);
        }

        var queue = priority == JiraPriority.User ? _user : _background;
        TaskCompletionSource<RequestLease> waiter;
        LinkedListNode<TaskCompletionSource<RequestLease>> node;

        lock (_lock)
        {
            if (CanRun(priority) && _user.Count == 0 && (priority == JiraPriority.User || _background.Count == 0))
            {
                return Task.FromResult(Grant(priority));
            }

            waiter = new TaskCompletionSource<RequestLease>(TaskCreationOptions.RunContinuationsAsynchronously);
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

    public void Release(RequestLease lease)
    {
        lock (_lock)
        {
            if (_leases.Remove(lease))
            {
                lease.Dispose();
                Dispatch();
            }
        }
    }

    /// <summary>
    /// Abandons the oldest background request that holds a slot, so the slot frees as soon as that request's call
    /// returns. False when no background request is running (a user request holds every slot; its own deadline
    /// applies).
    /// </summary>
    public bool AbandonOldestBackground()
    {
        lock (_lock)
        {
            var oldest = _leases.Where(lease => lease.Priority == JiraPriority.Background && !lease.IsAbandoned)
                .MinBy(lease => lease.Granted);

            oldest?.Abandon();

            return oldest is not null;
        }
    }

    private RequestLease Grant(JiraPriority priority)
    {
        var lease = new RequestLease(priority, Stopwatch.GetTimestamp());

        _leases.Add(lease);

        return lease;
    }

    private bool CanRun(JiraPriority priority) =>
        priority == JiraPriority.User ? _leases.Count < _capacity : _leases.Count < _backgroundLimit;

    private void Cancel(
        LinkedList<TaskCompletionSource<RequestLease>> queue,
        LinkedListNode<TaskCompletionSource<RequestLease>> node,
        TaskCompletionSource<RequestLease> waiter,
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
            GrantTo(user.Value, JiraPriority.User);
        }

        while (_background.First is { } background && _user.Count == 0 && CanRun(JiraPriority.Background))
        {
            _background.RemoveFirst();
            GrantTo(background.Value, JiraPriority.Background);
        }
    }

    private void GrantTo(TaskCompletionSource<RequestLease> waiter, JiraPriority priority)
    {
        var lease = Grant(priority);

        // A waiter cancelled in the meantime is already completed; its slot goes to the next one.
        if (!waiter.TrySetResult(lease))
        {
            _leases.Remove(lease);
            lease.Dispose();
        }
    }
}

/// <summary>One granted slot of the <see cref="RequestGate"/>. Release it through the gate when the call ends.</summary>
internal sealed class RequestLease : IDisposable
{
    private readonly CancellationTokenSource _abandoned = new();

    public RequestLease(JiraPriority priority, long granted)
    {
        Priority = priority;
        Granted = granted;
    }

    public JiraPriority Priority { get; }

    public long Granted { get; }

    public bool IsAbandoned => _abandoned.IsCancellationRequested;

    /// <summary>Cancelled when the gate took the slot back for a user request.</summary>
    public CancellationToken AbandonToken => _abandoned.Token;

    public void Abandon() => _abandoned.Cancel();

    public void Dispose() => _abandoned.Dispose();
}
