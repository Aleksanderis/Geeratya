using Geeratya.Platform.Terminal;

namespace Geeratya.Tui.Rendering;

/// <summary>Resolves roles for a colour tier.</summary>
public static class Palette
{
    public static ResolvedStyle Resolve(ColorSupport tier, PaletteRole role)
    {
        var entry = PaletteTable.Get(role);

        return tier switch
        {
            ColorSupport.TrueColor => new ResolvedStyle(
                tier,
                entry.TrueForeground,
                entry.TrueBackground,
                null,
                null,
                RoleStyle.None),
            ColorSupport.Basic16 => new ResolvedStyle(
                tier,
                Ansi16.ToRgb(entry.Foreground16),
                Ansi16.ToRgb(entry.Background16),
                entry.Foreground16,
                entry.Background16,
                RoleStyle.None),
            _ => new ResolvedStyle(tier, null, null, null, null, entry.NoColorStyle),
        };
    }
}
