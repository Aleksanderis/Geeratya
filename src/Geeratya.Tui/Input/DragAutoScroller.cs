using Geeratya.Core.Mouse;

namespace Geeratya.Tui.Input;

/// <summary>
/// The clock of an auto-scroll while a pointer rests outside the list with the button held: raises
/// <see cref="Stepped"/> every <see cref="DragAutoScroll.Interval"/> with the rows to move until stopped. The event
/// comes from the timer thread; the owner hops to the UI thread. No clock or timer lives in the state layer.
/// </summary>
public sealed class DragAutoScroller : IDisposable
{
    private readonly TimeProvider _time;
    private ITimer? _timer;
    private int _rows;

    public DragAutoScroller(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>Raised on a timer thread with the signed rows of the current speed.</summary>
    public event EventHandler<int>? Stepped;

    /// <summary>The rows the next step moves, zero when stopped.</summary>
    public int Rows => Volatile.Read(ref _rows);

    /// <summary>Scrolls at that speed (zero stops); the timer starts once and runs on while the speed changes.</summary>
    public void Start(int rows)
    {
        if (rows == 0)
        {
            Stop();

            return;
        }

        Volatile.Write(ref _rows, rows);
        _timer ??= _time.CreateTimer(OnTimer, null, DragAutoScroll.Interval, DragAutoScroll.Interval);
    }

    public void Stop()
    {
        Volatile.Write(ref _rows, 0);
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose()
    {
        Stop();
    }

    private void OnTimer(object? timerState)
    {
        var rows = Rows;

        if (rows != 0)
        {
            Stepped?.Invoke(this, rows);
        }
    }
}
