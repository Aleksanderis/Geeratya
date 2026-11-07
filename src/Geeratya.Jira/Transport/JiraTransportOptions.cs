using System.Reflection;

namespace Geeratya.Jira.Transport;

/// <summary>Tunables of a transport. Delay and jitter are injectable so tests never wait.</summary>
public sealed record JiraTransportOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Extra attempts after the first one, for idempotent requests only.</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>Cap of simultaneous requests to the site.</summary>
    public int MaxConcurrentRequests { get; init; } = 4;

    /// <summary>
    /// Slots of <see cref="MaxConcurrentRequests"/> that background requests never take (one always stays usable).
    /// </summary>
    public int ReservedForUser { get; init; } = 1;

    /// <summary>
    /// Background requests wait for the rate-limit window to reset once this few requests are left. User requests
    /// wait only when none are left.
    /// </summary>
    public int BackgroundThrottleRemaining { get; init; } = 10;

    public TimeSpan BaseBackoff { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Upper bound for any single wait, also for a larger <c>Retry-After</c>.</summary>
    public TimeSpan MaxWait { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>When set, calls to any other host are refused (live tests: <c>GEERATYA_LIVE_HOSTS</c>).</summary>
    public IReadOnlySet<string>? AllowedHosts { get; init; }

    /// <summary>Options for live tests: only the hosts in <c>GEERATYA_LIVE_HOSTS</c> are reachable.</summary>
    public static JiraTransportOptions ForLiveTests(Func<string, string?> environmentLookup) =>
        new() { AllowedHosts = JiraHostAllowList.Parse(environmentLookup(JiraHostAllowList.EnvironmentVariable)) };

    /// <summary>Product and build version only, never a host or an account.</summary>
    public string UserAgent { get; init; } = $"Geeratya/{BuildVersion()}";

    public TimeProvider Clock { get; init; } = TimeProvider.System;

    public Func<TimeSpan, CancellationToken, Task> Delay { get; init; } = Task.Delay;

    /// <summary>Returns a value in [0, 1).</summary>
    public Func<double> Jitter { get; init; } = Random.Shared.NextDouble;

    private static string BuildVersion()
    {
        var version = typeof(JiraTransportOptions).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0-dev";

        // A User-Agent product token has no spaces; drop any build metadata after '+'.
        var plus = version.IndexOf('+', StringComparison.Ordinal);

        return plus < 0 ? version : version[..plus];
    }
}
