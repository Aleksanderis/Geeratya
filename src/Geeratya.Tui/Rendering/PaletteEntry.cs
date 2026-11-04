namespace Geeratya.Tui.Rendering;

/// <summary>
/// How one role looks in every colour tier. <c>OnPage</c>: the role sits on the page background. <c>IsBar16</c>: the
/// role is part of the status bar, whose 16-colour fill (blue, or black on a light theme) is not dimmed behind an
/// overlay.
/// </summary>
public sealed record PaletteEntry(
    Rgb TrueForeground,
    Rgb TrueBackground,
    Ansi16Color Foreground16,
    Ansi16Color Background16,
    RoleStyle NoColorStyle,
    bool OnPage = false,
    bool IsBar16 = false);
