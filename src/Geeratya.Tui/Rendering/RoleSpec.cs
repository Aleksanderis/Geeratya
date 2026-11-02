namespace Geeratya.Tui.Rendering;

/// <summary>One role of the built-in themes: its 24-bit foreground in each theme, its 16-colour pick and its style.</summary>
public sealed record RoleSpec(
    PaletteRole Role,
    int Dark,
    int Light,
    int HighContrast,
    Ansi16Color Dark16,
    Ansi16Color Light16,
    RoleStyle Style);
