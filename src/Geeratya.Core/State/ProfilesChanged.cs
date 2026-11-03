using Geeratya.Core.Profiles;

namespace Geeratya.Core.State;

/// <summary>
/// The host saved the profile file (and token): the new catalog and a notice for the status line.
/// <paramref name="Reconnect"/> says the token of the active profile was replaced, so its connection is rebuilt.
/// </summary>
public sealed record ProfilesChanged(ProfileCatalog Catalog, string Message, bool Reconnect = false) : Command;
