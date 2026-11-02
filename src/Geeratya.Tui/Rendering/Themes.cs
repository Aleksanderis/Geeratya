namespace Geeratya.Tui.Rendering;

/// <summary>
/// The built-in themes: dark (the default), light, high contrast and ansi16 (only the 16 terminal colours: meant for
/// the 16-colour tier, where they are the terminal's own palette; in 24-bit it draws typical xterm values).
/// </summary>
public static class Themes
{
    public const string DarkName = "dark";
    public const string LightName = "light";
    public const string HighContrastName = "high-contrast";
    public const string Ansi16Name = "ansi16";

    private const RoleStyle None = RoleStyle.None;
    private const RoleStyle Bold = RoleStyle.Bold;
    private const RoleStyle Underline = RoleStyle.Underline;
    private const RoleStyle Reverse = RoleStyle.Reverse;
    private const RoleStyle Italic = RoleStyle.Italic;

    private static readonly RoleSpec[] _specs =
    [
        new(PaletteRole.Body, 0xD8DEE9, 0x2E3440, 0xFFFFFF, Ansi16Color.Gray, Ansi16Color.Black, None),
        new(PaletteRole.Status, 0xE5E9F0, 0x2E3440, 0x000000, Ansi16Color.White, Ansi16Color.White, Reverse),
        new(PaletteRole.Muted, 0x9AA5B8, 0x555F70, 0xC8C8C8, Ansi16Color.DarkGray, Ansi16Color.Blue, None),
        new(PaletteRole.Divider, 0x6B7587, 0x7F889A, 0xA0A0A0, Ansi16Color.DarkGray, Ansi16Color.DarkGray, None),
        new(PaletteRole.Error, 0xFF8080, 0xB3261E, 0xFF6B6B, Ansi16Color.BrightRed, Ansi16Color.Red, Bold),
        new(PaletteRole.Loading, 0xEBCB8B, 0x8A5A00, 0xFFD700, Ansi16Color.Yellow, Ansi16Color.Magenta, Underline),
        new(
            PaletteRole.Offline,
            0xD08770,
            0xA23E1D,
            0xFF9E5E,
            Ansi16Color.BrightMagenta,
            Ansi16Color.Red,
            Bold | Underline),
        new(
            PaletteRole.RateLimited,
            0xE5C07B,
            0x7A5200,
            0xFFEE58,
            Ansi16Color.BrightYellow,
            Ansi16Color.Magenta,
            Bold),
        new(
            PaletteRole.Stale,
            0xB48EAD,
            0x7B4B8A,
            0xE0A0FF,
            Ansi16Color.BrightCyan,
            Ansi16Color.Blue,
            Underline | Reverse),
        new(PaletteRole.Heading, 0x88C0D0, 0x1F6F8B, 0x00E5FF, Ansi16Color.BrightCyan, Ansi16Color.Blue, Bold),
        new(PaletteRole.Strong, 0xECEFF4, 0x1B1F27, 0xFFFFFF, Ansi16Color.White, Ansi16Color.Black, Bold),
        new(PaletteRole.Emphasis, 0xD8DEE9, 0x2E3440, 0xFFFFFF, Ansi16Color.Gray, Ansi16Color.Black, Italic),
        new(PaletteRole.Code, 0xA3BE8C, 0x2F6B1E, 0x7CFC7C, Ansi16Color.BrightGreen, Ansi16Color.Black, Reverse),
        new(PaletteRole.Link, 0x81A1C1, 0x1D5FA0, 0x66CCFF, Ansi16Color.BrightCyan, Ansi16Color.Blue, Underline),
        new(
            PaletteRole.Quote,
            0xA0AABE,
            0x555F70,
            0xDDDDDD,
            Ansi16Color.DarkGray,
            Ansi16Color.Blue,
            Italic | Underline),
        new(
            PaletteRole.Selected,
            0xECEFF4,
            0x1B1F27,
            0x000000,
            Ansi16Color.White,
            Ansi16Color.White,
            Reverse | Bold),
        new(PaletteRole.Key, 0xEBCB8B, 0x7A5200, 0xFFD700, Ansi16Color.BrightYellow, Ansi16Color.Magenta, Bold),
        new(PaletteRole.StatusToDo, 0xE5E9F0, 0x2E3440, 0xFFFFFF, Ansi16Color.White, Ansi16Color.Black, None),
        new(
            PaletteRole.StatusInProgress,
            0x88C0D0,
            0x1F6F8B,
            0x00E5FF,
            Ansi16Color.BrightCyan,
            Ansi16Color.Blue,
            Bold),
        new(
            PaletteRole.StatusDone,
            0xA3BE8C,
            0x2F6B1E,
            0x7CFC7C,
            Ansi16Color.BrightGreen,
            Ansi16Color.Black,
            Bold | Italic),
        new(
            PaletteRole.TypeEpic,
            0xB48EAD,
            0x7B4B8A,
            0xE0A0FF,
            Ansi16Color.BrightMagenta,
            Ansi16Color.Magenta,
            Bold),
        new(PaletteRole.TypeStory, 0xA3BE8C, 0x2F6B1E, 0x7CFC7C, Ansi16Color.BrightGreen, Ansi16Color.Black, Bold),
        new(PaletteRole.TypeBug, 0xE5848E, 0xB3261E, 0xFF6B6B, Ansi16Color.BrightRed, Ansi16Color.Red, Bold),
        new(PaletteRole.TypeTask, 0x81A1C1, 0x1D5FA0, 0x66CCFF, Ansi16Color.BrightCyan, Ansi16Color.Blue, Bold),
        new(PaletteRole.TypeSubtask, 0x8FBCBB, 0x1F6F6E, 0x40E0D0, Ansi16Color.Gray, Ansi16Color.Black, Bold),
        new(PaletteRole.FieldLabel, 0x81A1C1, 0x1D5FA0, 0x66CCFF, Ansi16Color.Cyan, Ansi16Color.Blue, None),
        new(PaletteRole.Editable, 0xEBCB8B, 0x7A5200, 0xFFD700, Ansi16Color.BrightYellow, Ansi16Color.Magenta, None),
    ];

    public static Theme Dark { get; } = Create(DarkName, ThemeKind.Dark);

    public static Theme Light { get; } = Create(LightName, ThemeKind.Light);

    public static Theme HighContrast { get; } = Create(HighContrastName, ThemeKind.HighContrast);

    public static Theme Basic16 { get; } = Create(Ansi16Name, ThemeKind.Ansi16);

    public static IReadOnlyList<Theme> All { get; } = [Dark, Light, HighContrast, Basic16];

    /// <summary>The built-in theme called <paramref name="name"/> (case-insensitive), or null.</summary>
    public static Theme? Find(string name)
    {
        return All.FirstOrDefault(theme => string.Equals(theme.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static Theme Create(string name, ThemeKind kind)
    {
        var entries = new Dictionary<PaletteRole, PaletteEntry>();

        foreach (var spec in _specs)
        {
            entries[spec.Role] = CreateEntry(spec, kind);
        }

        return new Theme(name, entries);
    }

    private static PaletteEntry CreateEntry(RoleSpec spec, ThemeKind kind)
    {
        var foreground16 = Foreground16(spec, kind);
        var ground = Background(spec.Role, kind);
        var background16 = ground.Basic;

        if (kind == ThemeKind.Ansi16)
        {
            return new PaletteEntry(
                Ansi16.ToRgb(foreground16),
                Ansi16.ToRgb(background16),
                foreground16,
                background16,
                spec.Style);
        }

        var foreground = kind switch
        {
            ThemeKind.Light => spec.Light,
            ThemeKind.HighContrast => spec.HighContrast,
            _ => spec.Dark,
        };

        return new PaletteEntry(Rgb.FromHex(foreground), ground.True, foreground16, background16, spec.Style);
    }

    private static Ansi16Color Foreground16(RoleSpec spec, ThemeKind kind)
    {
        if (kind == ThemeKind.Light)
        {
            return spec.Light16;
        }

        // Dark gray is too dim on the black of the high contrast theme.
        if (kind == ThemeKind.HighContrast && spec.Dark16 == Ansi16Color.DarkGray)
        {
            return Ansi16Color.Gray;
        }

        return spec.Dark16;
    }

    private static RoleGround Background(PaletteRole role, ThemeKind kind)
    {
        var barLike = role is PaletteRole.Status or PaletteRole.Selected;

        if (!barLike)
        {
            return kind switch
            {
                ThemeKind.Light => new RoleGround(Rgb.FromHex(0xFAFAFA), Ansi16Color.White),
                ThemeKind.HighContrast => new RoleGround(Rgb.FromHex(0x000000), Ansi16Color.Black),
                _ => new RoleGround(Rgb.FromHex(0x1E222A), Ansi16Color.Black),
            };
        }

        var selected = role == PaletteRole.Selected;

        return kind switch
        {
            ThemeKind.Light => new RoleGround(Rgb.FromHex(selected ? 0xB8C4D8 : 0xD8DEE9), Ansi16Color.Blue),
            ThemeKind.HighContrast => new RoleGround(Rgb.FromHex(selected ? 0xFFFF00 : 0xFFFFFF), Ansi16Color.Blue),
            _ => new RoleGround(Rgb.FromHex(selected ? 0x434C5E : 0x3B4252), Ansi16Color.Blue),
        };
    }
}
