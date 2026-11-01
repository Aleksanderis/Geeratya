using Geeratya.Core.Profiles;

namespace Geeratya.Core.State;

/// <summary>Store the token under the profile's credential key, then save the catalog.</summary>
public sealed record AddProfileEffect(Profile Profile, SecretText Token, ProfileCatalog Catalog) : Effect;
