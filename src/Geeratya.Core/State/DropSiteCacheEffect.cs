namespace Geeratya.Core.State;

/// <summary>Drop whatever the host cached for the site that is no longer active (connection, responses).</summary>
public sealed record DropSiteCacheEffect(string SiteUrl) : Effect;
