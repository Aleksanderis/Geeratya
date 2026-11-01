namespace Geeratya.Tui.Rendering;

/// <summary>The one place colours are decided: every role in the 24-bit, 16-colour and no-colour tiers.</summary>
public static class PaletteTable
{
    private const double TextContrast = 4.5;
    private const double DividerContrast = 3.0;

    private static readonly Rgb _ground = Rgb.FromHex(0x1E222A);
    private static readonly Rgb _bar = Rgb.FromHex(0x3B4252);

    private static readonly Dictionary<PaletteRole, PaletteEntry> _entries = new()
    {
        [PaletteRole.Body] = Create(0xD8DEE9, Ansi16Color.Gray, RoleStyle.None),
        [PaletteRole.Status] = new PaletteEntry(
            Rgb.FromHex(0xE5E9F0),
            _bar,
            Ansi16Color.White,
            Ansi16Color.Blue,
            RoleStyle.Reverse),
        [PaletteRole.Muted] = Create(0x9AA5B8, Ansi16Color.DarkGray, RoleStyle.None),
        [PaletteRole.Divider] = Create(0x6B7587, Ansi16Color.DarkGray, RoleStyle.None),
        [PaletteRole.Error] = Create(0xFF8080, Ansi16Color.BrightRed, RoleStyle.Bold),
        [PaletteRole.Loading] = Create(0xEBCB8B, Ansi16Color.Yellow, RoleStyle.Underline),
        [PaletteRole.Offline] = Create(0xD08770, Ansi16Color.BrightMagenta, RoleStyle.Bold | RoleStyle.Underline),
        [PaletteRole.RateLimited] = Create(0xE5C07B, Ansi16Color.BrightYellow, RoleStyle.Bold),
        [PaletteRole.Stale] = Create(0xB48EAD, Ansi16Color.BrightCyan, RoleStyle.Underline | RoleStyle.Reverse),
    };

    public static IReadOnlyList<PaletteRole> Roles { get; } = Enum.GetValues<PaletteRole>();

    public static PaletteEntry Get(PaletteRole role)
    {
        return _entries[role];
    }

    /// <summary>The contrast ratio a role must reach against its background: 3.0 for dividers, 4.5 for text.</summary>
    public static double RequiredContrast(PaletteRole role)
    {
        return role == PaletteRole.Divider ? DividerContrast : TextContrast;
    }

    private static PaletteEntry Create(int foreground, Ansi16Color foreground16, RoleStyle style)
    {
        return new PaletteEntry(Rgb.FromHex(foreground), _ground, foreground16, Ansi16Color.Black, style);
    }
}
