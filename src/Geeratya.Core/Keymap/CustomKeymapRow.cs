namespace Geeratya.Core.Keymap;

/// <summary>One change the user's <c>keymap.json</c> made: a binding added or changed, or a default removed.</summary>
/// <param name="Mode">The mode of the binding.</param>
/// <param name="Keys">The keys that were bound or unbound.</param>
/// <param name="Action">The action id now bound to the keys; null for a default that was removed.</param>
public sealed record CustomKeymapRow(InputMode Mode, string Keys, string? Action);
