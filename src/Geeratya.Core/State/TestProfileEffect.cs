using Geeratya.Core.Profiles;

namespace Geeratya.Core.State;

/// <summary>
/// Ask Jira who the account is, with the typed token or, when <paramref name="Token"/> is null, the one stored for
/// <paramref name="StoredKey"/>. Nothing is saved. Answered by <see cref="ProfileTested"/>.
/// </summary>
public sealed record TestProfileEffect(Profile Profile, SecretText? Token, string? StoredKey, bool AllowEnvironment)
    : Effect;
