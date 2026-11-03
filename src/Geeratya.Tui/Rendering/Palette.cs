using Geeratya.Core.Text;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Rendering;

/// <summary>Resolves roles of a theme for a colour tier.</summary>
public static class Palette
{
    private const double TintContrast = 3.0;

    public static ResolvedStyle Resolve(Theme theme, ColorSupport tier, PaletteRole role)
    {
        return Resolve(theme, tier, role, SpanGround.Page, true);
    }

    /// <summary>
    /// Resolves a role where it sits: on the page, on an overlay's raised surface (the page background of the role
    /// becomes the surface; roles with a fill of their own keep it) and/or dimmed behind an overlay (the page
    /// background becomes the scrim and text is blended towards it at 24 bits; a box under another overlay keeps its
    /// surface and only its text is dimmed; faint, or bright black when <paramref name="faintSupported"/> is false,
    /// at 16 colours; unchanged without colour). The ground never changes the page background at 16 colours or without colour.
    /// </summary>
    public static ResolvedStyle Resolve(
        Theme theme,
        ColorSupport tier,
        PaletteRole role,
        SpanGround ground,
        bool faintSupported)
    {
        var style = ResolveRole(theme, tier, role, ground, faintSupported);

        return (ground & (SpanGround.Selection | SpanGround.Range | SpanGround.Cursor)) == 0
            ? style
            : Tinted(theme, tier, style, ground, faintSupported);
    }

    private static ResolvedStyle ResolveRole(
        Theme theme,
        ColorSupport tier,
        PaletteRole role,
        SpanGround ground,
        bool faintSupported)
    {
        var entry = theme.Entry(role);
        var dim = ground.HasFlag(SpanGround.Dim);

        switch (tier)
        {
            case ColorSupport.TrueColor:
                var foreground = dim ? theme.Dimmed(entry.TrueForeground) : entry.TrueForeground;
                var background = PageBackground(theme, entry, ground);

                return new ResolvedStyle(foreground, background, null, null, ColourStyle(entry, true));
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
                return new ResolvedStyle(null, null, Ansi16Color.Gray, Ansi16Color.Black, LowTier(entry.NoColorStyle));
        }
    }

    // A span on the selected row or in the Visual range keeps its own colours over the tint where they stay readable
    // (contrast 3.0), else it takes the tint's text colour. Without colour the whole row is the selection role
    // (reversed). The cursor row of a range is underlined in every tier.
    private static ResolvedStyle Tinted(
        Theme theme,
        ColorSupport tier,
        ResolvedStyle style,
        SpanGround ground,
        bool faintSupported)
    {
        var underline = ground.HasFlag(SpanGround.Cursor) ? RoleStyle.Underline : RoleStyle.None;

        if (!ground.HasFlag(SpanGround.Range) && !ground.HasFlag(SpanGround.Selection))
        {
            return style with { Style = style.Style | underline };
        }

        var tintRole = ground.HasFlag(SpanGround.Range) ? PaletteRole.Range : PaletteRole.Selected;
        var tint = Resolve(theme, tier, tintRole, ground & SpanGround.Dim, faintSupported);

        switch (tier)
        {
            case ColorSupport.TrueColor:
                var foreground = Readable(style.Foreground, tint.Background, tint.Foreground);

                return tint with { Foreground = foreground, Style = style.Style | RoleStyle.Bold | underline };
            case ColorSupport.Basic16:
                var named = Readable16(theme, style.Foreground16, tint.Background16, tint.Foreground16);

                return tint with
                {
                    Foreground = Ansi16.ToRgb(named!.Value, theme.Variant),
                    Foreground16 = named,
                    Style = style.Style | RoleStyle.Bold | underline,
                };
            default:
                return tint with { Style = tint.Style | underline };
        }
    }

    private static Rgb? Readable(Rgb? own, Rgb? background, Rgb? fallback)
    {
        return own is { } value && background is { } ground && Contrast.Ratio(value, ground) >= TintContrast
            ? own
            : fallback;
    }

    private static Ansi16Color? Readable16(Theme theme, Ansi16Color? own, Ansi16Color? background, Ansi16Color? fallback)
    {
        if (own is not { } value || background is not { } ground)
        {
            return fallback;
        }

        var ratio = Contrast.Ratio(Ansi16.ToRgb(value, theme.Variant), Ansi16.ToRgb(ground, theme.Variant));

        return ratio >= TintContrast ? value : fallback;
    }

    // The page colour of a role becomes the scrim behind an overlay or the overlay's own fill; a fill of its own stays.
    private static Rgb PageBackground(Theme theme, PaletteEntry entry, SpanGround ground)
    {
        if (entry.TrueBackground != theme.PageBackground)
        {
            return entry.TrueBackground;
        }

        if (ground.HasFlag(SpanGround.Surface))
        {
            return theme.Surface;
        }

        return ground.HasFlag(SpanGround.Dim) ? theme.ScrimBackground : entry.TrueBackground;
    }

    // Emphasis, strong, links and quotes must show in the colour tiers too; reverse stays no-colour only, because
    // the colour tiers already give a role its own background.
    private static RoleStyle ColourStyle(PaletteEntry entry, bool truecolor = false)
    {
        var style = entry.NoColorStyle & ~RoleStyle.Reverse;

        return truecolor ? style & ~RoleStyle.UnderlineWithoutTint : LowTier(style);
    }

    // A role that is a tint at 24 bits is an underline where there is no tint to draw.
    private static RoleStyle LowTier(RoleStyle style)
    {
        return style.HasFlag(RoleStyle.UnderlineWithoutTint)
            ? (style & ~RoleStyle.UnderlineWithoutTint) | RoleStyle.Underline
            : style;
    }
}
