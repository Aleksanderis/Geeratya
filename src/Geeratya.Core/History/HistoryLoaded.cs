using Geeratya.Core.Profiles;
using Geeratya.Core.Sources;

namespace Geeratya.Core.History;

/// <summary>The stored history of a profile (after a read or a write); ignored when the profile is not active.</summary>
public sealed record HistoryLoaded(Profile Profile, SiteHistory History) : SourceCommand;
