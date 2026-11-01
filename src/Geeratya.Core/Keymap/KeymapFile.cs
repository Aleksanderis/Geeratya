namespace Geeratya.Core.Keymap;

/// <summary>The shape of <c>keymap.default.json</c>.</summary>
public sealed record KeymapFile(IReadOnlyList<KeyBinding> Bindings);
