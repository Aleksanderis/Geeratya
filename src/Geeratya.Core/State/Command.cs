namespace Geeratya.Core.State;

/// <summary>An input to the reducer.</summary>
public abstract record Command;

/// <summary>
/// A key in <see cref="Geeratya.Core.Keymap.KeyNotation"/> spelling, with the time it was pressed (the reducer reads
/// no clock; a submitted form needs the time for its write).
/// </summary>
public sealed record KeyPressed(string Key, DateTimeOffset At = default) : Command;

/// <summary>Opens Confirm mode for a destructive action.</summary>
public sealed record ConfirmRequested(ConfirmRequest Request) : Command;

/// <summary>Text pasted by the terminal (bracketed paste); fills the active Insert or Command field.</summary>
public sealed record TextPasted(string Text) : Command;

/// <summary>
/// An action id that a non-key input (the mouse table) asks for: it runs the handler the key would, repeated
/// <paramref name="Count"/> times for motions.
/// </summary>
public sealed record ActionRequested(string Action, int Count = 1, DateTimeOffset At = default) : Command;
