namespace Geeratya.Core.State;

/// <summary>An input to the reducer.</summary>
public abstract record Command;

/// <summary>
/// A key in <see cref="Geeratya.Core.Keymap.KeyNotation"/> spelling. <paramref name="At"/> is the time of the key
/// (the reducer reads no clock); writes started by a key carry it.
/// </summary>
public sealed record KeyPressed(string Key, DateTimeOffset At = default) : Command;

/// <summary>Opens Confirm mode for a destructive action.</summary>
public sealed record ConfirmRequested(ConfirmRequest Request) : Command;

/// <summary>Text pasted by the terminal (bracketed paste); fills the active Insert or Command field.</summary>
public sealed record TextPasted(string Text) : Command;
