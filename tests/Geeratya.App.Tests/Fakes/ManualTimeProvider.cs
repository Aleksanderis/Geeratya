namespace Geeratya.App.Tests.Fakes;

/// <summary>
/// A clock the test moves by hand: timers fire only when <see cref="Advance"/> passes their due time, so debounce and
/// slow-lookup tests need no sleeps. One-shot and periodic timers are supported.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private readonly object _lock = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Timers that are waiting for their due time.</summary>
    public int PendingTimers
    {
        get
        {
            lock (_lock)
            {
                return _timers.Count(timer => timer.IsArmed);
            }
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
        {
            return _now;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);

        lock (_lock)
        {
            _timers.Add(timer);
        }

        timer.Change(dueTime, period);

        return timer;
    }

    /// <summary>Moves the clock and runs the callbacks of the timers that became due, in due-time order.</summary>
    public void Advance(TimeSpan by)
    {
        List<ManualTimer> due;

        lock (_lock)
        {
            _now += by;
            due = [.. _timers.Where(timer => timer.IsDue(_now)).OrderBy(timer => timer.DueAt)];
        }

        foreach (var timer in due)
        {
            timer.Fire(GetUtcNow());
        }
    }

    private void Remove(ManualTimer timer)
    {
        lock (_lock)
        {
            _timers.Remove(timer);
        }
    }

    private sealed class ManualTimer : ITimer
    {
        private readonly ManualTimeProvider _owner;
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private TimeSpan _period = Timeout.InfiniteTimeSpan;

        public ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state)
        {
            _owner = owner;
            _callback = callback;
            _state = state;
        }

        public DateTimeOffset? DueAt { get; private set; }

        public bool IsArmed => DueAt is not null;

        public bool IsDue(DateTimeOffset now)
        {
            return DueAt is { } at && at <= now;
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _period = period;
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : _owner.GetUtcNow() + dueTime;

            return true;
        }

        public void Fire(DateTimeOffset now)
        {
            DueAt = _period > TimeSpan.Zero ? now + _period : null;
            _callback(_state);
        }

        public void Dispose()
        {
            DueAt = null;
            _owner.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
