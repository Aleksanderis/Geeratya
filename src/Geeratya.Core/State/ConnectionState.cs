namespace Geeratya.Core.State;

/// <summary>Whether the active profile's credentials work, as far as the last connection check knows.</summary>
public abstract record ConnectionState
{
    private protected ConnectionState()
    {
    }
}

/// <summary>No check has run for this session (no profile, or the start check has not been requested yet).</summary>
public sealed record Unchecked : ConnectionState;

/// <summary>A check is in flight.</summary>
public sealed record Checking : ConnectionState;

/// <summary>Jira accepted the credentials; <paramref name="DisplayName"/> is the account's display name.</summary>
public sealed record Connected(string DisplayName) : ConnectionState;

/// <summary>The check failed for <paramref name="Kind"/>; <paramref name="Hint"/> is the one-line text to show.</summary>
public sealed record ConnectionCheckFailure(ConnectionFailureKind Kind, string Hint) : ConnectionState;
