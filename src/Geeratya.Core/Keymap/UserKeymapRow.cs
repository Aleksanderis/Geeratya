namespace Geeratya.Core.Keymap;

/// <summary>One row of the user's <c>bindings</c>; every part may be missing in a bad file.</summary>
public sealed record UserKeymapRow(InputMode? Mode, string? Keys, string? Action);
