using Geeratya.Core.Profiles;

namespace Geeratya.Core.State;

/// <summary>
/// The result of a form's connection test for <paramref name="Profile"/> (the values that were tested);
/// <paramref name="Message"/> never contains the token.
/// </summary>
public sealed record ProfileTested(Profile Profile, bool Succeeded, string Message) : Command;
