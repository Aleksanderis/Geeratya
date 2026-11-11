using Geeratya.Jira.Transport;

namespace Geeratya.Core.Views;

/// <summary>
/// What a tab is doing. "Loaded" is <see cref="Idle"/> with rows; staleness is a separate flag on the tab, because
/// cached rows shown while the refresh runs are both stale and loading.
/// </summary>
public abstract record LoadState
{
    /// <summary>No request in flight.</summary>
    public static LoadState Idle { get; } = new IdleState();

    private protected LoadState()
    {
    }
}

/// <summary>No request in flight.</summary>
public sealed record IdleState : LoadState;

/// <summary>
/// A request is in flight; <paramref name="Background"/> says it runs in the background lane (a refresh by the clock).
/// <paramref name="Since"/> is when the clock first saw it (null until the next tick stamps it): a request older
/// than <see cref="LoadDeadline.Limit"/> is turned into a failure, so a reply that never comes cannot leave the
/// view loading for ever (#924).
/// </summary>
public sealed record Loading(LoadReason Reason, bool Background = false, DateTimeOffset? Since = null) : LoadState;

/// <summary>The last request failed; <paramref name="Reason"/> is what it was loading (a failed next page is retried as one).</summary>
public sealed record Failed(JiraErrorKind Kind, string Message, LoadReason Reason = LoadReason.Open) : LoadState;
