using Geeratya.Core.Profiles;

namespace Geeratya.Core.State;

/// <summary>The host saved the profile file (and token): the new catalog and a notice for the status line.</summary>
public sealed record ProfilesChanged(ProfileCatalog Catalog, string Message) : Command;
