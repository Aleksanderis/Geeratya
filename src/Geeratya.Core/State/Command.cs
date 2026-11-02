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

/// <summary>
/// A click that places the cursor: the pointer does what motions do for a key (<c>docs/ux-mouse.md</c>). On a list it
/// selects the row, on columns the card or the column header, in an opened issue the line.
/// </summary>
/// <param name="Column">The screen column of the click.</param>
/// <param name="Row">The row inside the body (the first row under the tab bar and banner is 0).</param>
/// <param name="Width">The screen width the body was drawn for; columns are laid out from it.</param>
public sealed record PointerPlaced(int Column, int Row, int Width) : Command;
