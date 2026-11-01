using Geeratya.Core.Profiles;

namespace Geeratya.Core.State;

/// <summary>
/// The host could not build the connection for <paramref name="Profile"/> (no token, bad site URL). Ignored when
/// that profile is no longer the active one, like any reply that arrives after a switch.
/// </summary>
public sealed record ConnectionFailed(Profile Profile, string Message) : Command;
