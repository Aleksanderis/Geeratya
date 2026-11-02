namespace Geeratya.Core.State;

/// <summary>Whether the active site can be reached. Driven by commands and failures, not by tab data.</summary>
public abstract record SiteLink
{
    private protected SiteLink()
    {
    }
}

/// <summary>The site answers.</summary>
public sealed record Online : SiteLink;

/// <summary>The site could not be reached since <paramref name="Since"/>.</summary>
// ReSharper disable once NotAccessedPositionalProperty.Global (read by the status line, #90)
public sealed record Offline(DateTimeOffset Since) : SiteLink;

/// <summary>Jira asked to wait until <paramref name="Until"/>.</summary>
// ReSharper disable once NotAccessedPositionalProperty.Global (read by the status line, #90)
public sealed record RateLimited(DateTimeOffset Until) : SiteLink;

/// <summary>Jira rejected the credentials.</summary>
public sealed record Rejected : SiteLink;
