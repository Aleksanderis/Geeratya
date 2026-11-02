namespace Geeratya.Core.State;

/// <summary>
/// Asks to open a link. Browse links must be on the host of the active site; with <paramref name="IsRemoteLink"/>
/// (Confluence and other remote links) any https host is allowed.
/// <see cref="Label"/> names what is opened (an issue key) for the status message.
/// </summary>
public sealed record OpenUrlRequested(string Url, bool IsRemoteLink, string? Label = null) : Command;
