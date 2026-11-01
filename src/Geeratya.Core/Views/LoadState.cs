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

/// <summary>A request is in flight.</summary>
public sealed record Loading(LoadReason Reason) : LoadState;

/// <summary>The last request failed.</summary>
public sealed record Failed(JiraErrorKind Kind, string Message) : LoadState;
