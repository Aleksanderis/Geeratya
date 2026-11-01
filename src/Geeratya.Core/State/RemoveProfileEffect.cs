using Geeratya.Core.Profiles;

namespace Geeratya.Core.State;

/// <summary>
/// Save the catalog without the profile and delete its token when no other profile uses it.
/// <paramref name="WasActive"/> says the removed profile was the active one, so the host reports the new one.
/// </summary>
public sealed record RemoveProfileEffect(
    Profile Profile,
    bool DeleteToken,
    ProfileCatalog Catalog,
    bool WasActive) : Effect;
