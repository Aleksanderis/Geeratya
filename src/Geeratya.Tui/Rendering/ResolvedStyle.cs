namespace Geeratya.Tui.Rendering;

/// <summary>
/// A role resolved for one tier. The no-colour tier has no RGB values: plain gray on black, meaning rests on style.
/// </summary>
public sealed record ResolvedStyle(
    Rgb? Foreground,
    Rgb? Background,
    Ansi16Color? Foreground16,
    Ansi16Color? Background16,
    RoleStyle Style);
