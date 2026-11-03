using Geeratya.Core.Profiles;

namespace Geeratya.Core.State;

/// <summary>The token source of each profile, keyed by <see cref="Profile.CredentialKey"/>.</summary>
public sealed record TokenSourcesLoaded(IReadOnlyDictionary<string, TokenSourceKind> Sources) : Command;
