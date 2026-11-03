using Geeratya.Core.Keymap;
using Geeratya.Platform.Terminal;

namespace Geeratya.Tui.Rendering;

/// <summary>What a body renderer may read besides the state: the keymap and the glyph set.</summary>
public sealed record BodyContext(KeymapTable Keymap, GlyphSet Glyphs = GlyphSet.Unicode);
