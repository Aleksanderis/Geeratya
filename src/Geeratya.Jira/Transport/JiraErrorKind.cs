namespace Geeratya.Jira.Transport;

/// <summary>The typed failure classes of a Jira call.</summary>
public enum JiraErrorKind
{
    BadRequest,
    Unauthorized,
    Forbidden,
    NotFound,
    RateLimited,
    ServerError,
    Timeout,
    Network,
    HostRefused,
    InvalidResponse,
    Unexpected,
}
