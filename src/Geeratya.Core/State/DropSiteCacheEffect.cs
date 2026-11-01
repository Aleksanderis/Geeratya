using Geeratya.Core.Profiles;

namespace Geeratya.Core.State;

/// <summary>
/// Drop whatever the host cached for the profile that is no longer active (connection, responses). Keyed by the
/// whole profile, so two profiles on one site with different accounts never share a cache. The host handler lands
/// with the first cache; until then the shell ignores this effect.
/// </summary>
public sealed record DropSiteCacheEffect(Profile Profile) : Effect;
