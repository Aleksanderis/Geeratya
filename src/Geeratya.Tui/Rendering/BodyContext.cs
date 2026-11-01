using Geeratya.Core.Keymap;

namespace Geeratya.Tui.Rendering;

/// <summary>What a body renderer may read besides the state: generated help text and the keymap.</summary>
public sealed record BodyContext(IReadOnlyList<string> HelpLines, KeymapTable Keymap);
