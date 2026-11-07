namespace Geeratya.Core.Views;

/// <summary>
/// What the network says about a tab (<c>docs/internal/architecture.md</c>, "Network states"). One kind per tab, the first
/// that applies in the order of <c>TabStatus.Of</c>.
/// </summary>
public enum NetworkStatusKind
{
    /// <summary>Nothing to report.</summary>
    Fresh,

    /// <summary>A request of the user lane is in flight.</summary>
    Loading,

    /// <summary>The rows are old (cache, or a refresh that has not arrived yet).</summary>
    Stale,

    /// <summary>The site cannot be reached; the last rows stay.</summary>
    Offline,

    /// <summary>Jira asked to wait.</summary>
    RateLimited,

    /// <summary>A reply that is an error, or rejected credentials.</summary>
    Error,
}
