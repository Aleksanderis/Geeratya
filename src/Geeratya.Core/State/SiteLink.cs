namespace Geeratya.Core.State;

/// <summary>Whether the active site can be reached. Driven by commands and failures, not by tab data.</summary>
public abstract record SiteLink
{
    /// <summary>
    /// The link state for the transport's rate-limit wait: <see cref="RateLimited"/> while it ends in the future
    /// (<paramref name="until"/> set), <see cref="Online"/> when it ended. The host posts it as a command from the
    /// transport's event, which arrives on a non-UI thread.
    /// </summary>
    public static SiteLink FromRateLimit(DateTimeOffset? until)
    {
        return until is { } end ? new RateLimited(end) : new Online();
    }

    private protected SiteLink()
    {
    }
}

/// <summary>The site answers.</summary>
public sealed record Online : SiteLink;

/// <summary>The site could not be reached since <paramref name="Since"/>.</summary>
// ReSharper disable once NotAccessedPositionalProperty.Global (the link state record keeps when it began; compared in tests)
public sealed record Offline(DateTimeOffset Since) : SiteLink;

/// <summary>Jira asked to wait until <paramref name="Until"/>.</summary>
public sealed record RateLimited(DateTimeOffset Until) : SiteLink;

/// <summary>Jira rejected the credentials.</summary>
public sealed record Rejected : SiteLink;
