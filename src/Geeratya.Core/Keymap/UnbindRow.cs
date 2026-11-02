namespace Geeratya.Core.Keymap;

/// <summary>One default binding the user removes, named by mode and keys (either may be missing in a bad file).</summary>
public sealed record UnbindRow(InputMode? Mode, string? Keys);
