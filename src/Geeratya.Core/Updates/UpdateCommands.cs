using Geeratya.Core.State;

namespace Geeratya.Core.Updates;

/// <summary>The shell drew its first frame: look for a newer release in the background.</summary>
public sealed record UpdateCheckRequested : Command;

/// <summary>The check found a newer release (a check that finds none, or fails, says nothing).</summary>
public sealed record UpdateAvailable(UpdateNotice Notice) : Command;

/// <summary>
/// Look for a newer release: remove a leftover old binary of an earlier update, then, when
/// <paramref name="Enabled"/> (the <c>update.check</c> setting), ask at most once a day. Answered with
/// <see cref="UpdateAvailable"/> or nothing.
/// </summary>
public sealed record CheckForUpdateEffect(bool Enabled) : Effect;
