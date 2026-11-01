namespace Geeratya.Core.Keymap;

/// <summary>One keymap row: in <see cref="Mode"/>, the keys <see cref="Keys"/> run <see cref="Action"/>.</summary>
/// <param name="Mode">The mode the binding is active in.</param>
/// <param name="Keys">Key notation, several keys separated by a space (<c>g g</c>).</param>
/// <param name="Action">The action id, a key of <see cref="ActionCatalog"/>.</param>
public sealed record KeyBinding(InputMode Mode, string Keys, string Action);
