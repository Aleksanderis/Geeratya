using Geeratya.Core.Profiles;
using Geeratya.Core.Sources;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;

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
/// <param name="Sources">The boards and saved filters the source picker offers, loaded on demand.</param>
public sealed record SiteSession(
    string? ProfileName,
    string? SiteUrl,
    IReadOnlyList<TabState> Tabs,
    TabId? Active,
    SiteLink Link,
    ConnectionState Connection,
    SourceLists Sources)
{
    /// <summary>
    /// The writes that were sent and not yet settled, from every tab. Site level on purpose: exclusion must see all
    /// tabs, and a tab closed after its write was sent must not forget it (<c>docs/architecture.md</c>, "Running
    /// operations"). Dropped with the session on a profile change.
    /// </summary>
    public IReadOnlyList<Operation> Operations { get; init; } = [];

    /// <summary>The delete whose sub-tasks are being counted before the confirm opens; null when none.</summary>
    public PendingDelete? PendingDelete { get; init; }

    public static SiteSession None { get; } = new(null, null, [], null, new Online(), new Unchecked(), SourceLists.Empty);

    /// <summary>A session with no tabs for <paramref name="profile"/>.</summary>
    public static SiteSession Empty(Profile? profile)
    {
        return profile is null ? None : None with { ProfileName = profile.Name, SiteUrl = profile.SiteUrl };
    }
}
