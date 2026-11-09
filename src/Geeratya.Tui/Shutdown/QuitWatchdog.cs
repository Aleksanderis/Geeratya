namespace Geeratya.Tui.Shutdown;

/// <summary>
/// The hard way out. A quit normally ends the loop within milliseconds; when it does not (the UI thread is stuck on
/// something that never returns), the deadline passes and the process exits anyway, so <c>q</c> or a termination
/// signal never leaves a terminal that cannot be closed. A second termination signal exits at once.
/// </summary>
public sealed class QuitWatchdog : IDisposable
{
    private readonly TimeProvider _clock;
    private readonly IProcessExit _exit;
    private readonly Lock _lock = new();
    private ITimer? _timer;
    private DateTimeOffset _deadline = DateTimeOffset.MaxValue;
    private bool _signalled;

    public QuitWatchdog(TimeProvider clock, IProcessExit exit)
    {
        _clock = clock;
        _exit = exit;
    }

    /// <summary>
    /// Makes sure the process ends within <paramref name="grace"/>. An earlier deadline stays; a later one never
    /// postpones it.
    /// </summary>
    public void Arm(TimeSpan grace)
    {
        lock (_lock)
        {
            var deadline = _clock.GetUtcNow() + grace;

            if (deadline >= _deadline)
            {
                return;
            }

            _deadline = deadline;
            _timer?.Dispose();
            _timer = _clock.CreateTimer(Fire, null, grace, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// A termination signal arrived (SIGINT, SIGTERM, SIGHUP; not the UI thread). Returns true for the first one,
    /// which arms the deadline and leaves the stopping to the caller; the second ends the process at once.
    /// </summary>
    public bool Terminate(TimeSpan grace)
    {
        lock (_lock)
        {
            if (_signalled)
            {
                _exit.EndProcess();

                return false;
            }

            _signalled = true;
        }

        Arm(grace);

        return true;
    }

    /// <summary>The application ended by itself: the deadline no longer applies.</summary>
    public void Disarm()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
            _deadline = DateTimeOffset.MaxValue;
        }
    }

    public void Dispose()
    {
        Disarm();
    }

    private void Fire(object? state)
    {
        _exit.EndProcess();
    }
}
