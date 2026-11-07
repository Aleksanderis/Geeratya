namespace Geeratya.Tui;

/// <summary>
/// Holds UI work while the terminal belongs to an outside editor and hands it back, in order, on release. Thread-safe
/// on its own; the host takes its own lock around "ask this queue, else invoke on the application" so the flag and
/// the application it points at change together.
/// </summary>
public sealed class PausableWorkGate
{
    private readonly Lock _gate = new();
    private readonly Queue<Action> _held = new();
    private bool _paused;

    public bool IsPaused
    {
        get
        {
            lock (_gate)
            {
                return _paused;
            }
        }
    }

    /// <summary>From now on <see cref="TryHold"/> keeps the work.</summary>
    public void Pause()
    {
        lock (_gate)
        {
            _paused = true;
        }
    }

    /// <summary>Keeps the work and returns true while paused; returns false when it should run now.</summary>
    public bool TryHold(Action work)
    {
        lock (_gate)
        {
            if (!_paused)
            {
                return false;
            }

            _held.Enqueue(work);

            return true;
        }
    }

    /// <summary>Ends the pause and returns the held work, oldest first.</summary>
    public IReadOnlyList<Action> Release()
    {
        lock (_gate)
        {
            _paused = false;

            var work = _held.ToArray();

            _held.Clear();

            return work;
        }
    }
}
