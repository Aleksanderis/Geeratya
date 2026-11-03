using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Rendering;

/// <summary>Resolves roles of a theme for a colour tier.</summary>
public static class Palette
{
    public static ResolvedStyle Resolve(Theme theme, ColorSupport tier, PaletteRole role)
    {
        var entry = theme.Entry(role);

        return tier switch
        {
            ColorSupport.TrueColor => new ResolvedStyle(
                entry.TrueForeground,
                entry.TrueBackground,
                null,
                null,
                ColourStyle(entry)),
            ColorSupport.Basic16 => new ResolvedStyle(
                Ansi16.ToRgb(entry.Foreground16, theme.Variant),
                Ansi16.ToRgb(entry.Background16, theme.Variant),
                entry.Foreground16,
                entry.Background16,
                ColourStyle(entry)),
            _ => new ResolvedStyle(null, null, Ansi16Color.Gray, Ansi16Color.Black, entry.NoColorStyle),
        };
    }

    // Emphasis, strong, links and quotes must show in the colour tiers too; reverse stays no-colour only, because
    // the colour tiers already give a role its own background.
    private static RoleStyle ColourStyle(PaletteEntry entry)
    {
        return entry.NoColorStyle & ~RoleStyle.Reverse;
    }
}
