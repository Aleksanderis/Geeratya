using Geeratya.Core.Text;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Rendering;

/// <summary>Resolves roles of a theme for a colour tier.</summary>
public static class Palette
{
    public static ResolvedStyle Resolve(Theme theme, ColorSupport tier, PaletteRole role)
    {
        return Resolve(theme, tier, role, SpanGround.Page, true);
    }

    /// <summary>
    /// Resolves a role where it sits: on the page, on an overlay's raised surface (the page background of the role
    /// becomes the surface; roles with a fill of their own keep it) and/or dimmed behind an overlay (text blended
    /// towards the page at 24 bits; faint, or bright black when <paramref name="faintSupported"/> is false, at 16
    /// colours;
    /// unchanged without colour). The ground never changes the page background at 16 colours or without colour.
    /// </summary>
    public static ResolvedStyle Resolve(
        Theme theme,
        ColorSupport tier,
        PaletteRole role,
        SpanGround ground,
        bool faintSupported)
    {
        var entry = theme.Entry(role);
        var onPage = entry.TrueBackground == theme.PageBackground;
        var dim = ground.HasFlag(SpanGround.Dim);

        switch (tier)
        {
            case ColorSupport.TrueColor:
                var foreground = dim ? theme.Dimmed(entry.TrueForeground) : entry.TrueForeground;
                var background = onPage && ground.HasFlag(SpanGround.Surface) ? theme.Surface : entry.TrueBackground;

                return new ResolvedStyle(foreground, background, null, null, ColourStyle(entry));
            case ColorSupport.Basic16:
                var plain = entry.Background16 is Ansi16Color.Black or Ansi16Color.White;
                var faint = dim && plain && faintSupported;
                var named = dim && plain && !faintSupported ? Ansi16Color.DarkGray : entry.Foreground16;

                return new ResolvedStyle(
                    Ansi16.ToRgb(named, theme.Variant),
                    Ansi16.ToRgb(entry.Background16, theme.Variant),
                    named,
                    entry.Background16,
                    faint ? ColourStyle(entry) | RoleStyle.Faint : ColourStyle(entry));
            default:
                return new ResolvedStyle(null, null, Ansi16Color.Gray, Ansi16Color.Black, entry.NoColorStyle);
        }
    }

    // Emphasis, strong, links and quotes must show in the colour tiers too; reverse stays no-colour only, because
    // the colour tiers already give a role its own background.
    private static RoleStyle ColourStyle(PaletteEntry entry)
    {
        return entry.NoColorStyle & ~RoleStyle.Reverse;
    }
}
