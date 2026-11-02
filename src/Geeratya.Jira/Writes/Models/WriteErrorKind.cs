namespace Geeratya.Jira.Writes.Models;

/// <summary>The typed failure classes of a Jira write.</summary>
public enum WriteErrorKind
{
    /// <summary>400: Jira rejected the content; <see cref="WriteError.FieldErrors"/> and messages say why.</summary>
    Validation,
    Unauthorized,

    /// <summary>403: the account may not do this.</summary>
    Permission,

    /// <summary>404 or 410: gone, or hidden (the two cannot be told apart).</summary>
    NotFound,

    /// <summary>409: the issue changed meanwhile or a workflow rule refuses the change.</summary>
    Conflict,

    /// <summary>429: not processed; retry later (already retried when a <c>Retry-After</c> was given).</summary>
    RateLimited,

    /// <summary>No reply in time. The write may or may not have happened.</summary>
    Timeout,

    /// <summary>The request was sent but the reply is missing or unreadable (network, 5xx, bad body).</summary>
    UnknownOutcome,

    /// <summary>The request was not sent (host not allowed).</summary>
    NotSent,
}
