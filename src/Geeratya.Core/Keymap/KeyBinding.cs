using Geeratya.Core.State;

namespace Geeratya.Core.Keymap;

/// <summary>One row of the keymap: in <see cref="Mode"/>, the key sequence <see cref="Keys"/> runs <see cref="Action"/>.</summary>
/// <param name="Mode">The mode the binding is active in.</param>
/// <param name="Keys">Key notation, several keys separated by a space (<c>g g</c>).</param>
/// <param name="Action">The action id, a key of <see cref="ActionCatalog"/>.</param>
public sealed record KeyBinding(InputMode Mode, string Keys, string Action);
