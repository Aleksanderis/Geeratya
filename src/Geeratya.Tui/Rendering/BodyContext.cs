using Geeratya.Core.Keymap;
using Geeratya.Platform.Terminal;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// What a body renderer may read besides the state: the keymap, the glyph set and the time of the frame (the epoch
/// in the print frame).
/// </summary>
public sealed record BodyContext(
    KeymapTable Keymap,
    GlyphSet Glyphs = GlyphSet.Unicode,
    DateTimeOffset Now = default);
