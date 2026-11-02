namespace Geeratya.Core.Keymap;

/// <summary>One default binding the user removes, named by mode and keys.</summary>
public sealed record UnbindRow(InputMode Mode, string Keys);
