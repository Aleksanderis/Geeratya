using Geeratya.Core.Profiles;
using Geeratya.Core.Sources;

namespace Geeratya.Core.SavedViews;

/// <summary>
/// The stored saved views of a profile (after a read or a write); ignored when the profile is not active. Version
/// grows with every file operation of the host, so a reply that arrives late (older version) is dropped.
/// </summary>
public sealed record SavedViewsLoaded(Profile Profile, SavedViewList Views, long Version) : SourceCommand;
