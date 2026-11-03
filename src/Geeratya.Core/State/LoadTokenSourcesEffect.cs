using Geeratya.Core.Profiles;

namespace Geeratya.Core.State;

/// <summary>
/// Find out where the token of each of <paramref name="Profiles"/> comes from (secret store, environment variable,
/// none) for the profile manager's details pane. Answered by <see cref="TokenSourcesLoaded"/>; no token leaves the host.
/// </summary>
public sealed record LoadTokenSourcesEffect(IReadOnlyList<Profile> Profiles) : Effect;
