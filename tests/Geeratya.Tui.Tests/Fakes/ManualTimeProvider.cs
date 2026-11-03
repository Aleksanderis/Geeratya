namespace Geeratya.Tui.Tests.Fakes;

/// <summary>A clock whose timers fire only when the test says so, and that counts the timers it created.</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private TimerCallback? _callback;
    private object? _state;

    public int Created { get; private set; }

    public int Disposed { get; private set; }

    public void Fire()
    {
        _callback?.Invoke(_state);
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        _callback = callback;
        _state = state;
        Created++;

        return new ManualTimer(this);
    }

    private sealed class ManualTimer : ITimer
    {
        private readonly ManualTimeProvider _owner;

        public ManualTimer(ManualTimeProvider owner)
        {
            _owner = owner;
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            return true;
        }

        public void Dispose()
        {
            _owner._callback = null;
            _owner.Disposed++;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
