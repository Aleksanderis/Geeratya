using Geeratya.Core.Profiles;
using Geeratya.Core.State;

namespace Geeratya.Core.Startup;

/// <summary>The shell drew its first frame: read the remembered tabs of the active profile.</summary>
public sealed record StartupRequested : Command;

/// <summary>The remembered tabs of <paramref name="Profile"/>; ignored when it is no longer the active one.</summary>
public sealed record TabsLoaded(Profile Profile, RememberedTabs Tabs) : Command;

/// <summary>The tabs file could not be written; the status line says so.</summary>
public sealed record TabsSaveFailed(string Message) : Command;

/// <summary>Read the profile's remembered tabs; answered with <see cref="TabsLoaded"/>.</summary>
public sealed record LoadTabsEffect(Profile Profile) : Effect;

/// <summary>Write the profile's remembered tabs (the latest request wins).</summary>
public sealed record SaveTabsEffect(Profile Profile, RememberedTabs Tabs) : Effect;
