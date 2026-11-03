
namespace Geeratya.Core.State;

/// <summary>The result of a form's connection test; <paramref name="Message"/> never contains the token.</summary>
public sealed record ProfileTested(bool Succeeded, string Message) : Command;
