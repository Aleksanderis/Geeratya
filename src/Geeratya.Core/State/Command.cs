namespace Geeratya.Core.State;

/// <summary>An input to the reducer.</summary>
public abstract record Command;

/// <summary>A key in <see cref="Geeratya.Core.Keymap.KeyNotation"/> spelling.</summary>
public sealed record KeyPressed(string Key) : Command;

/// <summary>Opens Confirm mode for a destructive action.</summary>
public sealed record ConfirmRequested(ConfirmRequest Request) : Command;
