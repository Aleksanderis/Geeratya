using Geeratya.Core.Profiles;

namespace Geeratya.Core.State;

/// <summary>
/// Drop whatever the host cached for the profile that is no longer active (connection, responses). Keyed by the
/// whole profile, so two profiles on one site with different accounts never share a cache. The host closes the
/// connection of that profile and drops its caches.
/// </summary>
public sealed record DropSiteCacheEffect(Profile Profile) : Effect;
