using Geeratya.Core.State;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Views;

/// <summary>The network status of one tab: the kind, plus what the banner needs to word it.</summary>
/// <param name="Kind">The status.</param>
/// <param name="Until">The end of a rate-limit wait, otherwise null.</param>
/// <param name="Failure">The failed load behind an offline or error status, otherwise null.</param>
/// <param name="CredentialsRejected">The error is a rejected token (a 401 on this tab or on any request of the site).</param>
public sealed record TabStatus(
    NetworkStatusKind Kind,
    DateTimeOffset? Until = null,
    Failed? Failure = null,
    bool CredentialsRejected = false)
{
    /// <summary>Nothing to report.</summary>
    public static TabStatus Fresh { get; } = new(NetworkStatusKind.Fresh);

    /// <summary>Whether the banner text counts time (so the host redraws on a clock tick).</summary>
    public bool ShowsTime => Kind is NetworkStatusKind.Offline or NetworkStatusKind.RateLimited
        or NetworkStatusKind.Stale;

    /// <summary>
    /// The status of <paramref name="tab"/> on a site in state <paramref name="link"/> at <paramref name="now"/>.
    /// Pure. Order: rejected credentials, rate-limit wait, offline, other errors, stale, loading.
    /// </summary>
    public static TabStatus Of(TabState tab, SiteLink link, DateTimeOffset now)
    {
        var failure = tab.Load as Failed;

        if (link is Rejected || failure is { Kind: JiraErrorKind.Unauthorized })
        {
            return new TabStatus(NetworkStatusKind.Error, null, failure, true);
        }

        if (link is RateLimited limited && limited.Until > now)
        {
            return new TabStatus(NetworkStatusKind.RateLimited, limited.Until, failure);
        }

        if (link is Offline || IsUnreachable(failure))
        {
            return new TabStatus(NetworkStatusKind.Offline, null, failure);
        }

        if (failure is not null)
        {
            return new TabStatus(NetworkStatusKind.Error, null, failure);
        }

        if (tab.IsStale)
        {
            return new TabStatus(NetworkStatusKind.Stale);
        }

        // A refresh by the clock is not announced: it would flash the status line every minute.
        return tab.Load is Loading { Background: false } ? new TabStatus(NetworkStatusKind.Loading) : Fresh;
    }

    /// <summary>The status of the active tab of <paramref name="session"/>; fresh without one.</summary>
    public static TabStatus Of(SiteSession session, DateTimeOffset now)
    {
        var tab = session.Active is { } id ? session.Tabs.FirstOrDefault(candidate => candidate.Id == id) : null;

        return tab is null ? Fresh : Of(tab, session.Link, now);
    }

    private static bool IsUnreachable(Failed? failure)
    {
        return failure?.Kind is JiraErrorKind.Network or JiraErrorKind.Timeout or JiraErrorKind.HostRefused;
    }
}
