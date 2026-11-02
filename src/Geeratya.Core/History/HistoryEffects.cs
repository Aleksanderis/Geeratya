using Geeratya.Core.Profiles;
using Geeratya.Core.State;

namespace Geeratya.Core.History;

/// <summary>Read the profile's history from disk; answered with <see cref="HistoryLoaded"/>.</summary>
public sealed record LoadHistoryEffect(Profile Profile) : Effect;

/// <summary>Remember a JQL string or issue key for the profile; answered with <see cref="HistoryLoaded"/>.</summary>
public sealed record RecordHistoryEffect(Profile Profile, HistoryKind Kind, string Text) : Effect;

/// <summary>Delete the profile's history file.</summary>
public sealed record ClearHistoryEffect(Profile Profile) : Effect;
