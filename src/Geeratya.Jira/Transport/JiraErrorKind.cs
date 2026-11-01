namespace Geeratya.Jira.Transport;

/// <summary>The typed failure classes of a Jira call.</summary>
public enum JiraErrorKind
{
    BadRequest,
    Unauthorized,
    Forbidden,
    NotFound,

    /// <summary>A paging token was rejected as bad or expired; reload from the first page.</summary>
    PageExpired,
    RateLimited,
    ServerError,
    Timeout,
    Network,
    HostRefused,
    InvalidResponse,
    Unexpected,
}
