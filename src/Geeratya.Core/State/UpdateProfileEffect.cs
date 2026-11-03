using Geeratya.Core.Profiles;

namespace Geeratya.Core.State;

/// <summary>
/// Save an edited profile. <paramref name="NewToken"/> is null when the stored token is kept; when the site or account
/// changed, the kept token moves to the new credential key. <paramref name="OldKeyShared"/> says another profile still
/// uses the old key, so its token and local data stay.
/// </summary>
public sealed record UpdateProfileEffect(
    Profile Old,
    Profile New,
    SecretText? NewToken,
    ProfileCatalog Catalog,
    bool OldKeyShared) : Effect;
