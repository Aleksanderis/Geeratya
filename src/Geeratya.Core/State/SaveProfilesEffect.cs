using Geeratya.Core.Profiles;

namespace Geeratya.Core.State;

/// <summary>Save the catalog (for example a new active profile) and answer with the message.</summary>
public sealed record SaveProfilesEffect(ProfileCatalog Catalog, string Message) : Effect;
