using System.Text.Json.Serialization;

namespace Geeratya.Core.Profiles;

/// <summary>The saved profiles and which one is active. Immutable; changes return a new catalog.</summary>
public sealed record ProfileCatalog(string? ActiveName, IReadOnlyList<Profile> Profiles)
{
    public static ProfileCatalog Empty { get; } = new(null, []);

    /// <summary>The active profile, or null when none is marked or the mark points at nothing.</summary>
    [JsonIgnore]
    public Profile? Active => Find(ActiveName);

    public Profile? Find(string? name)
    {
        return Profiles.FirstOrDefault(profile => profile.Name == name);
    }

    /// <summary>Adds the profile, or replaces the one with the same name. The first profile becomes active.</summary>
    public ProfileCatalog With(Profile profile)
    {
        var active = Active is null ? profile.Name : ActiveName;

        if (Find(profile.Name) is null)
        {
            return new ProfileCatalog(active, [.. Profiles, profile]);
        }

        var replaced = Profiles.Select(existing => existing.Name == profile.Name ? profile : existing).ToList();

        return new ProfileCatalog(active, replaced);
    }

    /// <summary>Removes the profile; when it was active, the first remaining one becomes active.</summary>
    public ProfileCatalog Without(string name)
    {
        var remaining = Profiles.Where(existing => existing.Name != name).ToList();

        if (ActiveName != name)
        {
            return new ProfileCatalog(ActiveName, remaining);
        }

        return new ProfileCatalog(remaining.FirstOrDefault()?.Name, remaining);
    }

    /// <summary>Marks the named profile active; an unknown name leaves the catalog unchanged.</summary>
    public ProfileCatalog Activate(string name)
    {
        return Find(name) is null ? this : this with { ActiveName = name };
    }
}
