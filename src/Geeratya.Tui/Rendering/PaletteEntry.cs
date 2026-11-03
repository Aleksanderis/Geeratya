namespace Geeratya.Tui.Rendering;

/// <summary>How one role looks in every colour tier. <c>OnPage</c>: the role sits on the page background.</summary>
public sealed record PaletteEntry(
    Rgb TrueForeground,
    Rgb TrueBackground,
    Ansi16Color Foreground16,
    Ansi16Color Background16,
    RoleStyle NoColorStyle,
    bool OnPage = false);
