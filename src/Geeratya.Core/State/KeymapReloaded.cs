namespace Geeratya.Core.State;

/// <summary>The keymap was reloaded; <paramref name="Message"/> is the status line (success or the problems).</summary>
public sealed record KeymapReloaded(string Message) : Command;
