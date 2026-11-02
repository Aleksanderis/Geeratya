namespace Geeratya.Core.Keymap;

/// <summary>The shape of the user's <c>keymap.json</c>: rows that add or replace bindings, and rows that remove defaults.</summary>
/// <param name="Bindings">Rows that bind keys; a row replaces the default row with the same mode and keys.</param>
/// <param name="Unbind">Default rows (mode and keys) to remove.</param>
public sealed record UserKeymapFile(IReadOnlyList<KeyBinding>? Bindings, IReadOnlyList<UnbindRow>? Unbind);
