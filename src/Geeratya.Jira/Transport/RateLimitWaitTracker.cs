namespace Geeratya.Jira.Transport;

/// <summary>
/// Tracks the rate-limit waits in progress on one transport (proactive throttle and <c>Retry-After</c>) so a UI can
/// show "waiting for Jira rate limit". The state is the latest end of any active wait, or null when none is waiting.
/// </summary>
public sealed class RateLimitWaitTracker
{
    private readonly object _lock = new();
    private readonly List<DateTimeOffset> _ends = [];

    public event Action? Changed;

    public DateTimeOffset? Until
    {
        get
        {
            lock (_lock)
            {
                return _ends.Count == 0 ? null : _ends.Max();
            }
        }
    }

    /// <summary>Runs <paramref name="wait"/> while it is published as a rate-limit wait ending at <paramref name="end"/>.</summary>
    public async Task RunAsync(DateTimeOffset end, Func<Task> wait)
    {
        lock (_lock)
        {
            _ends.Add(end);
        }

        try
        {
            Raise();
            await wait();
        }
        finally
        {
            lock (_lock)
            {
                _ends.Remove(end);
            }

            Raise();
        }
    }

    /// <summary>A failing handler must never fail a Jira call, so its exception is dropped.</summary>
    private void Raise()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception)
        {
            // Handlers belong to the UI; the request result is not theirs to change.
        }
    }
}
