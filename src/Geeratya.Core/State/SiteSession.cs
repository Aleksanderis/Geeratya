using Geeratya.Core.Profiles;
using Geeratya.Core.Views;

namespace Geeratya.Core.State;

/// <summary>
/// Everything that belongs to the active profile's site: the open tabs and the link state. It is replaced by
/// <see cref="Empty"/> whenever the active profile changes, so nothing of one site is shown for another. The data
/// cache is not part of it (<c>docs/architecture.md</c>, "Cache and refresh").
/// </summary>
/// <param name="ProfileName">The profile this session belongs to, or null when no profile is active.</param>
/// <param name="SiteUrl">The site of that profile, or null.</param>
/// <param name="Tabs">The tabs open on that site, in open order.</param>
/// <param name="Active">The active tab, null when there is none (start screen).</param>
/// <param name="Link">Whether the site can be reached.</param>
/// <param name="Connection">The result of the last credential check of this profile.</param>
public sealed record SiteSession(
    string? ProfileName,
    string? SiteUrl,
    IReadOnlyList<TabState> Tabs,
    TabId? Active,
    // ReSharper disable once NotAccessedPositionalProperty.Global (read by the status line, #90)
    SiteLink Link,
    ConnectionState Connection)
{
    public static SiteSession None { get; } = new(null, null, [], null, new Online(), new Unchecked());

    /// <summary>A session with no tabs for <paramref name="profile"/>.</summary>
    public static SiteSession Empty(Profile? profile)
    {
        return profile is null ? None : None with { ProfileName = profile.Name, SiteUrl = profile.SiteUrl };
    }
}
