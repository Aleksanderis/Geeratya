namespace Geeratya.Tui.Rendering;

/// <summary>
/// A role resolved for one tier. The no-colour tier has no RGB values: plain gray on black, meaning rests on style.
/// With <paramref name="DefaultBackground"/> the cell keeps the terminal's own background (the colour values stay as
/// the theme's page colour for contrast checks, but the surfaces draw the default).
/// </summary>
public sealed record ResolvedStyle(
    Rgb? Foreground,
    Rgb? Background,
    Ansi16Color? Foreground16,
    Ansi16Color? Background16,
    RoleStyle Style,
    bool DefaultBackground = false);
