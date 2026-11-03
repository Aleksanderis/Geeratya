namespace Geeratya.Core.State;

/// <summary>The keymap was reloaded; <paramref name="Message"/> is the status line (success or the problems);
/// <paramref name="Problems"/> are the skipped rows, each kept in the Messages history.</summary>
public sealed record KeymapReloaded(string Message, IReadOnlyList<string>? Problems = null) : Command;
