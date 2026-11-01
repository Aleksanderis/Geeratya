namespace Geeratya.Jira.Transport;

/// <summary>
/// Tracks the rate-limit waits in progress on one transport (proactive throttle and <c>Retry-After</c>) so a UI can
/// show "waiting for Jira rate limit". The state is the latest end of any active wait, or null when none is waiting.
/// </summary>
internal sealed class RateLimitWaitTracker
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
        Add(end);

        try
        {
            await wait();
        }
        finally
        {
            Remove(end);
        }
    }

    private void Add(DateTimeOffset end)
    {
        lock (_lock)
        {
            _ends.Add(end);
        }

        Changed?.Invoke();
    }

    private void Remove(DateTimeOffset end)
    {
        lock (_lock)
        {
            _ends.Remove(end);
        }

        Changed?.Invoke();
    }
}
