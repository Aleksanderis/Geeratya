namespace Geeratya.Core.State;

/// <summary>
/// The host tried a theme. <paramref name="Applied"/> says it is now drawn (then it is saved as the user's choice);
/// <paramref name="Message"/> is the status line either way.
/// </summary>
public sealed record ThemeApplied(string Name, bool Applied, string Message) : Command;
