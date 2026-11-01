using Geeratya.Core.Profiles;
using Geeratya.Core.Views;

namespace Geeratya.Core.State;

/// <summary>
/// Everything that belongs to the active profile's site: the open views and the cached data. It is replaced by
/// <see cref="Empty"/> whenever the active profile changes, so nothing of one site is shown for another.
/// </summary>
/// <param name="ProfileName">The profile this session belongs to, or null when no profile is active.</param>
/// <param name="SiteUrl">The site of that profile, or null.</param>
/// <param name="Views">The views (tabs) open on that site.</param>
/// <param name="Cache">The cached data of that site by key.</param>
public sealed record SiteSession(
    string? ProfileName,
    string? SiteUrl,
    IReadOnlyList<ViewDefinition> Views,
    IReadOnlyDictionary<string, string> Cache)
{
    public static SiteSession None { get; } = new(null, null, [], new Dictionary<string, string>());

    /// <summary>A session with no views and no cache for <paramref name="profile"/>.</summary>
    public static SiteSession Empty(Profile? profile)
    {
        return profile is null ? None : None with { ProfileName = profile.Name, SiteUrl = profile.SiteUrl };
    }

    /// <summary>True when <paramref name="profile"/> is the profile and site this session was built for.</summary>
    public bool Belongs(Profile? profile)
    {
        return profile?.Name == ProfileName && profile?.SiteUrl == SiteUrl;
    }
}
