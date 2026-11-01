using Geeratya.Platform.Terminal;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// A role resolved for one tier. Colours are null in the no-colour tier, where only the style is used.
/// </summary>
public sealed record ResolvedStyle(
    ColorSupport Tier,
    Rgb? Foreground,
    Rgb? Background,
    Ansi16Color? Foreground16,
    Ansi16Color? Background16,
    RoleStyle Style);
