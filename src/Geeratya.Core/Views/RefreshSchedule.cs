namespace Geeratya.Core.Views;

/// <summary>
/// The refresh timings (<c>docs/architecture.md</c>, "Cache and refresh"). Pure: every method takes the time it
/// works from, the reducer never reads a clock.
/// </summary>
public static class RefreshSchedule
{
    /// <summary>How old the data of the active tab may get before the next tick refreshes it.</summary>
    public static TimeSpan Interval { get; } = TimeSpan.FromSeconds(60);

    /// <summary>How old the data may be when its tab is switched to before it is refreshed.</summary>
    public static TimeSpan OnSwitch { get; } = TimeSpan.FromSeconds(30);

    /// <summary>The longest wait after repeated failures.</summary>
    public static TimeSpan MaxBackoff { get; } = TimeSpan.FromSeconds(300);

    /// <summary>When the next automatic refresh is due after rows arrived at <paramref name="loadedAt"/>.</summary>
    public static DateTimeOffset AfterSuccess(DateTimeOffset loadedAt)
    {
        return loadedAt + Interval;
    }

    /// <summary>
    /// When the next automatic refresh is due after the <paramref name="failures"/>-th failure in a row at
    /// <paramref name="failedAt"/>: 60, 120, 240, then 300 s.
    /// </summary>
    public static DateTimeOffset AfterFailure(DateTimeOffset failedAt, int failures)
    {
        return failedAt + Backoff(failures);
    }

    /// <summary>The wait after the <paramref name="failures"/>-th failure in a row (at least 1).</summary>
    public static TimeSpan Backoff(int failures)
    {
        var doublings = Math.Clamp(failures - 1, 0, 8);
        var wait = TimeSpan.FromTicks(Interval.Ticks << doublings);

        return wait > MaxBackoff ? MaxBackoff : wait;
    }

    /// <summary>
    /// The schedule of a tab that is switched to: its data counts as due once it is <see cref="OnSwitch"/> old. A
    /// backing-off tab keeps its backoff.
    /// </summary>
    public static DateTimeOffset? OnActivation(DateTimeOffset? nextRefreshAt, DateTimeOffset? loadedAt, int failures)
    {
        if (failures > 0 || loadedAt is not { } loaded || nextRefreshAt is not { } next)
        {
            return nextRefreshAt;
        }

        var soon = loaded + OnSwitch;

        return soon < next ? soon : next;
    }
}
