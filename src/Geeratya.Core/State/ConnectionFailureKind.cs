namespace Geeratya.Core.State;

/// <summary>Why a connection check failed; each kind has its own hint text.</summary>
public enum ConnectionFailureKind
{
    /// <summary>The connection could not be built: no token, a bad site URL, a failing secret store.</summary>
    Setup,

    /// <summary>401: the token or the email is wrong (Jira does not say which).</summary>
    Unauthorized,
    Forbidden,
    Network,
    Timeout,
    HostRefused,
    RateLimited,
    Other,
}
