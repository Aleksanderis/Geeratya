using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>The one place colours are decided: every role in the 24-bit, 16-colour and no-colour tiers.</summary>
public static class PaletteTable
{
    private const double TextContrast = 4.5;
    private const double DividerContrast = 3.0;

    private static readonly Rgb _ground = Rgb.FromHex(0x1E222A);
    private static readonly Rgb _bar = Rgb.FromHex(0x3B4252);
    private static readonly Rgb _selection = Rgb.FromHex(0x434C5E);

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
        [PaletteRole.Heading] = Create(0x88C0D0, Ansi16Color.BrightCyan, RoleStyle.Bold),
        [PaletteRole.Strong] = Create(0xECEFF4, Ansi16Color.White, RoleStyle.Bold),
        [PaletteRole.Emphasis] = Create(0xD8DEE9, Ansi16Color.Gray, RoleStyle.Italic),
        [PaletteRole.Code] = Create(0xA3BE8C, Ansi16Color.BrightGreen, RoleStyle.Reverse),
        [PaletteRole.Link] = Create(0x81A1C1, Ansi16Color.BrightCyan, RoleStyle.Underline),
        [PaletteRole.Quote] = Create(0xA0AABE, Ansi16Color.DarkGray, RoleStyle.Italic | RoleStyle.Underline),
        [PaletteRole.Selected] = new PaletteEntry(
            Rgb.FromHex(0xECEFF4),
            _selection,
            Ansi16Color.White,
            Ansi16Color.Blue,
            RoleStyle.Reverse | RoleStyle.Bold),
        [PaletteRole.Key] = Create(0xEBCB8B, Ansi16Color.BrightYellow, RoleStyle.Bold),
        [PaletteRole.StatusToDo] = Create(0xE5E9F0, Ansi16Color.White, RoleStyle.None),
        [PaletteRole.StatusInProgress] = Create(0x88C0D0, Ansi16Color.BrightCyan, RoleStyle.Bold),
        [PaletteRole.StatusDone] = Create(0xA3BE8C, Ansi16Color.BrightGreen, RoleStyle.Bold | RoleStyle.Italic),
        [PaletteRole.FieldLabel] = Create(0x81A1C1, Ansi16Color.Cyan, RoleStyle.None),
        [PaletteRole.Editable] = Create(0xEBCB8B, Ansi16Color.BrightYellow, RoleStyle.None),
    };

    private static readonly Dictionary<TextRole, PaletteRole> _textRoles = new()
    {
        [TextRole.Body] = PaletteRole.Body,
        [TextRole.Muted] = PaletteRole.Muted,
        [TextRole.Heading] = PaletteRole.Heading,
        [TextRole.Strong] = PaletteRole.Strong,
        [TextRole.Emphasis] = PaletteRole.Emphasis,
        [TextRole.Code] = PaletteRole.Code,
        [TextRole.Link] = PaletteRole.Link,
        [TextRole.Quote] = PaletteRole.Quote,
        [TextRole.Selected] = PaletteRole.Selected,
        [TextRole.Key] = PaletteRole.Key,
        [TextRole.StatusToDo] = PaletteRole.StatusToDo,
        [TextRole.StatusInProgress] = PaletteRole.StatusInProgress,
        [TextRole.StatusDone] = PaletteRole.StatusDone,
        [TextRole.Error] = PaletteRole.Error,
        [TextRole.Loading] = PaletteRole.Loading,
        [TextRole.Offline] = PaletteRole.Offline,
        [TextRole.RateLimited] = PaletteRole.RateLimited,
        [TextRole.Stale] = PaletteRole.Stale,
        [TextRole.FieldLabel] = PaletteRole.FieldLabel,
        [TextRole.Editable] = PaletteRole.Editable,
    };

    public static IReadOnlyList<PaletteRole> Roles { get; } = Enum.GetValues<PaletteRole>();

    /// <summary>The palette role that draws a renderer-agnostic text role.</summary>
    public static PaletteRole For(TextRole role)
    {
        return _textRoles[role];
    }

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
