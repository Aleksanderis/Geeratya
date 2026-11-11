namespace Geeratya.Tui.Shutdown;

/// <summary>
/// Notices a UI thread that stopped turning its loop, from a timer on another thread, and reports it with the last
/// work it was seen starting. The loop turns about 25 times a second even when idle, so a few seconds without a beat
/// is a stall (#883). Time is measured with the monotonic timestamp, not the wall clock, so a clock change never
/// shows up as a stall; the Linux monotonic clock stops while the machine sleeps, so a sleep is not one either.
/// Other systems may still count sleep as time (not verified, #941). While an outside editor owns the terminal no
/// loop runs: <see cref="Suspend"/> covers it (#940).
/// </summary>
public sealed class UiStallMonitor : IDisposable
{
    private static readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(1);

    private readonly TimeProvider _clock;
    private readonly IStallReport _report;
    private readonly TimeSpan _threshold;
    private readonly Lock _lock = new();
    private long _lastBeat;
    private long _stallStart;
    private string _lastWork = "none";
    private bool _suspended;
    private bool _reported;
    private ITimer? _timer;

    public UiStallMonitor(TimeProvider clock, IStallReport report, TimeSpan threshold)
    {
        _clock = clock;
        _report = report;
        _threshold = threshold;
        _lastBeat = clock.GetTimestamp();
    }

    /// <summary>Starts the periodic check.</summary>
    public void Start()
    {
        _timer ??= _clock.CreateTimer(Check, null, _checkInterval, _checkInterval);
    }

    /// <summary>The UI thread is alive (a loop iteration).</summary>
    public void Beat()
    {
        Volatile.Write(ref _lastBeat, _clock.GetTimestamp());
    }

    /// <summary>Names the work the UI thread starts, so a stall can say what it was doing.</summary>
    public void Note(string work)
    {
        Volatile.Write(ref _lastWork, work);
    }

    /// <summary>No loop runs for now (an outside editor): nothing is reported until <see cref="Resume"/>.</summary>
    public void Suspend()
    {
        lock (_lock)
        {
            _suspended = true;
            _reported = false;
        }
    }

    public void Resume()
    {
        Beat();

        lock (_lock)
        {
            _suspended = false;
        }
    }

    /// <summary>One check; the timer calls it, tests call it with a fake clock.</summary>
    public void Check(object? state = null)
    {
        lock (_lock)
        {
            if (_suspended)
            {
                return;
            }

            var silent = Since(Volatile.Read(ref _lastBeat));

            if (silent >= _threshold && !_reported)
            {
                _reported = true;
                _stallStart = Volatile.Read(ref _lastBeat);
                _report.Stalled(silent, Volatile.Read(ref _lastWork));
            }
            else if (silent < _threshold && _reported)
            {
                _reported = false;
                _report.Recovered(Since(_stallStart));
            }
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private TimeSpan Since(long timestamp)
    {
        return _clock.GetElapsedTime(timestamp);
    }
}
