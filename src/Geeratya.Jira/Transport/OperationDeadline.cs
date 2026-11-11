namespace Geeratya.Jira.Transport;

/// <summary>
/// The overall deadline of one call to <see cref="JiraTransport"/>: a token that is cancelled when the deadline
/// passes or the caller cancels, and that can be pushed back by the time spent in a rate-limit wait.
/// </summary>
internal sealed class OperationDeadline : IDisposable
{
    private readonly CancellationTokenSource _source;
    private readonly TimeProvider _clock;
    private readonly object _lock = new();
    private DateTimeOffset _end;

    public OperationDeadline(TimeSpan limit, TimeProvider clock, CancellationToken callerToken)
    {
        _clock = clock;
        _source = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        _end = clock.GetUtcNow() + limit;
        _source.CancelAfter(limit);
    }

    public CancellationToken Token => _source.Token;

    /// <summary>Pushes the deadline back by <paramref name="time"/> (a wait Jira asked for).</summary>
    public void Extend(TimeSpan time)
    {
        lock (_lock)
        {
            _end += time;

            // A deadline that already passed stays passed.
            if (!_source.IsCancellationRequested)
            {
                _source.CancelAfter((_end - _clock.GetUtcNow()) is { Ticks: > 0 } left ? left : TimeSpan.Zero);
            }
        }
    }

    public void Dispose() => _source.Dispose();
}
