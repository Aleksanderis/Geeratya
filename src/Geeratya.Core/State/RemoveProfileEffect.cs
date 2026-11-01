using Geeratya.Core.Profiles;

namespace Geeratya.Core.State;

/// <summary>Save the catalog without the profile and delete its token when no other profile uses it.</summary>
public sealed record RemoveProfileEffect(Profile Profile, bool DeleteToken, ProfileCatalog Catalog) : Effect;
