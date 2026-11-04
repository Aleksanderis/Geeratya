using Geeratya.Core.Text;
using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Rendering;

/// <summary>The one place colours are decided: every role in the 24-bit, 16-colour and no-colour tiers.</summary>
public static class PaletteTable
{
    private const double TextContrast = 4.5;
    private const double DividerContrast = 3.0;
    private const double HighContrast = 7.0;
    private const double RangeAccentShare = 0.35;
    private const double RangeAccentStep = 0.05;
    private const double RangeTextShare = 0.9;

    private static readonly Dictionary<PaletteRole, RoleSpec> _specs = new()
    {
        [PaletteRole.Body] = new(ThemeColor.BaseForeground, ThemeColor.BaseBackground, RoleStyle.None),
        [PaletteRole.Status] = new(
            ThemeColor.BaseForeground,
            ThemeColor.BarBackground,
            RoleStyle.Reverse,
            RoleGround.OnBlue),
        [PaletteRole.Muted] = new(ThemeColor.Muted, ThemeColor.BaseBackground, RoleStyle.None),
        [PaletteRole.Divider] = new(ThemeColor.Border, ThemeColor.BaseBackground, RoleStyle.None),
        [PaletteRole.Error] = new(ThemeColor.Bad, ThemeColor.BaseBackground, RoleStyle.Bold),
        [PaletteRole.Loading] = new(ThemeColor.Info, ThemeColor.BaseBackground, RoleStyle.Underline),
        [PaletteRole.Offline] = new(
            ThemeColor.Warn,
            ThemeColor.BaseBackground,
            RoleStyle.Bold | RoleStyle.Underline),
        [PaletteRole.RateLimited] = new(ThemeColor.Warn, ThemeColor.BaseBackground, RoleStyle.Bold),
        [PaletteRole.Warning] = new(ThemeColor.Warn, ThemeColor.BaseBackground, RoleStyle.Bold),
        [PaletteRole.Success] = new(ThemeColor.Good, ThemeColor.BaseBackground, RoleStyle.Bold),
        [PaletteRole.SuccessChip] = new(
            ThemeColor.BaseBackground,
            ThemeColor.Good,
            RoleStyle.Reverse | RoleStyle.Bold,
            RoleGround.Chip),
        [PaletteRole.Stale] = new(
            ThemeColor.Remote,
            ThemeColor.BaseBackground,
            RoleStyle.Underline | RoleStyle.Reverse),
        [PaletteRole.Heading] = new(ThemeColor.Accent, ThemeColor.BaseBackground, RoleStyle.Bold),
        [PaletteRole.Strong] = new(
            ThemeColor.BaseForeground,
            ThemeColor.BaseBackground,
            RoleStyle.Bold,
            RoleGround.Strong),
        [PaletteRole.Emphasis] = new(ThemeColor.BaseForeground, ThemeColor.BaseBackground, RoleStyle.Italic),
        [PaletteRole.Code] = new(ThemeColor.Good, ThemeColor.BaseBackground, RoleStyle.Reverse),
        [PaletteRole.Link] = new(ThemeColor.Accent, ThemeColor.BaseBackground, RoleStyle.Underline),
        [PaletteRole.Quote] = new(
            ThemeColor.Muted,
            ThemeColor.BaseBackground,
            RoleStyle.Italic | RoleStyle.Underline),
        [PaletteRole.Selected] = new(
            ThemeColor.BaseForeground,
            ThemeColor.Selection,
            RoleStyle.Reverse | RoleStyle.Bold,
            RoleGround.OnBlue),
        [PaletteRole.Key] = new(ThemeColor.Warn, ThemeColor.BaseBackground, RoleStyle.Bold),
        [PaletteRole.StatusToDo] = new(ThemeColor.BaseForeground, ThemeColor.BaseBackground, RoleStyle.None),
        [PaletteRole.StatusInProgress] = new(ThemeColor.Accent, ThemeColor.BaseBackground, RoleStyle.Bold),
        [PaletteRole.StatusDone] = new(ThemeColor.Good, ThemeColor.BaseBackground, RoleStyle.Bold),
        [PaletteRole.TypeEpic] = new(ThemeColor.Remote, ThemeColor.BaseBackground, RoleStyle.Bold),
        [PaletteRole.TypeStory] = new(ThemeColor.Good, ThemeColor.BaseBackground, RoleStyle.Bold),
        [PaletteRole.TypeBug] = new(ThemeColor.Bad, ThemeColor.BaseBackground, RoleStyle.Bold),
        [PaletteRole.TypeTask] = new(ThemeColor.Accent, ThemeColor.BaseBackground, RoleStyle.Bold),
        [PaletteRole.TypeSubtask] = new(ThemeColor.Info, ThemeColor.BaseBackground, RoleStyle.Bold),
        [PaletteRole.FieldLabel] = new(ThemeColor.Info, ThemeColor.BaseBackground, RoleStyle.None),
        [PaletteRole.Editable] = new(ThemeColor.Warn, ThemeColor.BaseBackground, RoleStyle.None),
        [PaletteRole.BarMuted] = new(
            ThemeColor.Muted,
            ThemeColor.BarBackground,
            RoleStyle.Reverse,
            RoleGround.OnBlue),
        [PaletteRole.BarKey] = new(
            ThemeColor.Warn,
            ThemeColor.BarBackground,
            RoleStyle.Reverse | RoleStyle.Bold,
            RoleGround.OnBlue),
        [PaletteRole.BarError] = new(
            ThemeColor.Bad,
            ThemeColor.BarBackground,
            RoleStyle.Reverse | RoleStyle.Bold),
        [PaletteRole.BarWarning] = new(
            ThemeColor.Warn,
            ThemeColor.BarBackground,
            RoleStyle.Reverse | RoleStyle.Bold),
        [PaletteRole.BarSuccess] = new(
            ThemeColor.Good,
            ThemeColor.BarBackground,
            RoleStyle.Reverse | RoleStyle.Bold),
        [PaletteRole.TabActive] = Chip(ThemeColor.Accent),
        [PaletteRole.BadgeNormal] = Chip(ThemeColor.Accent),
        [PaletteRole.BadgeVisual] = Chip(ThemeColor.Remote),
        [PaletteRole.BadgeInsert] = Chip(ThemeColor.Good),
        [PaletteRole.BadgeCommand] = Chip(ThemeColor.Warn),
        [PaletteRole.BadgeConfirm] = Chip(ThemeColor.Bad),
        [PaletteRole.BadgeHelp] = Chip(ThemeColor.Info),
        [PaletteRole.OverlayFrame] = new(ThemeColor.Accent, ThemeColor.BaseBackground, RoleStyle.Bold),
        [PaletteRole.OverlayFrameWarn] = new(ThemeColor.Warn, ThemeColor.BaseBackground, RoleStyle.Bold),
        [PaletteRole.OverlayFrameBad] = new(ThemeColor.Bad, ThemeColor.BaseBackground, RoleStyle.Bold),
        [PaletteRole.OverlayChip] = new(
            ThemeColor.BaseBackground,
            ThemeColor.Accent,
            RoleStyle.Reverse | RoleStyle.Bold,
            RoleGround.Chip),
        [PaletteRole.OverlayChipWarn] = new(
            ThemeColor.BaseBackground,
            ThemeColor.Warn,
            RoleStyle.Reverse | RoleStyle.Bold,
            RoleGround.Chip),
        [PaletteRole.OverlayChipBad] = new(
            ThemeColor.BaseBackground,
            ThemeColor.Bad,
            RoleStyle.Reverse | RoleStyle.Bold,
            RoleGround.Chip),
        [PaletteRole.FrameFocus] = new(ThemeColor.Accent, ThemeColor.BaseBackground, RoleStyle.Bold),
        [PaletteRole.ChipFocus] = new(
            ThemeColor.BaseBackground,
            ThemeColor.Accent,
            RoleStyle.Reverse | RoleStyle.Bold,
            RoleGround.Chip),
        [PaletteRole.SelectedDim] = new(
            ThemeColor.BaseForeground,
            ThemeColor.Selection,
            RoleStyle.UnderlineWithoutTint),
        [PaletteRole.OverlayCount] = new(ThemeColor.Muted, ThemeColor.BaseBackground, RoleStyle.None),
        [PaletteRole.Range] = new(
            ThemeColor.BaseForeground,
            ThemeColor.Selection,
            RoleStyle.Reverse | RoleStyle.Bold,
            RoleGround.Range),
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
        [TextRole.TypeEpic] = PaletteRole.TypeEpic,
        [TextRole.TypeStory] = PaletteRole.TypeStory,
        [TextRole.TypeBug] = PaletteRole.TypeBug,
        [TextRole.TypeTask] = PaletteRole.TypeTask,
        [TextRole.TypeSubtask] = PaletteRole.TypeSubtask,
        [TextRole.Error] = PaletteRole.Error,
        [TextRole.Loading] = PaletteRole.Loading,
        [TextRole.Offline] = PaletteRole.Offline,
        [TextRole.RateLimited] = PaletteRole.RateLimited,
        [TextRole.Warning] = PaletteRole.Warning,
        [TextRole.Stale] = PaletteRole.Stale,
        [TextRole.FieldLabel] = PaletteRole.FieldLabel,
        [TextRole.Editable] = PaletteRole.Editable,
        [TextRole.BarText] = PaletteRole.Status,
        [TextRole.BarMuted] = PaletteRole.BarMuted,
        [TextRole.TabDim] = PaletteRole.BarMuted,
        [TextRole.BarKey] = PaletteRole.BarKey,
        [TextRole.BarError] = PaletteRole.BarError,
        [TextRole.BarWarning] = PaletteRole.BarWarning,
        [TextRole.BarSuccess] = PaletteRole.BarSuccess,
        [TextRole.BarSuccessChip] = PaletteRole.SuccessChip,
        [TextRole.TabActive] = PaletteRole.TabActive,
        [TextRole.BadgeNormal] = PaletteRole.BadgeNormal,
        [TextRole.BadgeVisual] = PaletteRole.BadgeVisual,
        [TextRole.BadgeInsert] = PaletteRole.BadgeInsert,
        [TextRole.BadgeCommand] = PaletteRole.BadgeCommand,
        [TextRole.BadgeConfirm] = PaletteRole.BadgeConfirm,
        [TextRole.BadgeHelp] = PaletteRole.BadgeHelp,
        [TextRole.OverlayFrame] = PaletteRole.OverlayFrame,
        [TextRole.OverlayFrameWarn] = PaletteRole.OverlayFrameWarn,
        [TextRole.OverlayFrameBad] = PaletteRole.OverlayFrameBad,
        [TextRole.OverlayChip] = PaletteRole.OverlayChip,
        [TextRole.OverlayChipWarn] = PaletteRole.OverlayChipWarn,
        [TextRole.OverlayChipBad] = PaletteRole.OverlayChipBad,
        [TextRole.Border] = PaletteRole.Divider,
        [TextRole.FrameFocus] = PaletteRole.FrameFocus,
        [TextRole.ChipFocus] = PaletteRole.ChipFocus,
        [TextRole.SelectedDim] = PaletteRole.SelectedDim,
        [TextRole.OverlayCount] = PaletteRole.OverlayCount,
        [TextRole.Range] = PaletteRole.Range,
    };

    public static IReadOnlyList<PaletteRole> Roles { get; } = Enum.GetValues<PaletteRole>();

    /// <summary>The palette role that draws a renderer-agnostic text role.</summary>
    public static PaletteRole For(TextRole role)
    {
        return _textRoles[role];
    }

    /// <summary>The contrast ratio a role must reach against its background: 3.0 for dividers, 4.5 for text.</summary>
    public static double RequiredContrast(PaletteRole role)
    {
        return role == PaletteRole.Divider ? DividerContrast : TextContrast;
    }

    /// <summary>The entry of every role of a theme: its colours at 24 bits, the 16-colour table of its variant.</summary>
    public static IReadOnlyDictionary<PaletteRole, PaletteEntry> Build(Theme theme)
    {
        var entries = new Dictionary<PaletteRole, PaletteEntry>();

        foreach (var (role, spec) in _specs)
        {
            entries[role] = new PaletteEntry(
                role == PaletteRole.Range ? RangeText(theme) : theme.Colors[spec.Foreground],
                BackgroundOf(theme, role, spec),
                Foreground16(theme, spec),
                Background16(theme, spec),
                spec.Style,
                IsOnPage(spec));
        }

        return entries;
    }

    // A role on the page background (no fill of its own): the roles that keep the terminal's background when the
    // theme is drawn transparent. Tints (selection, range), the bars and the chips are fills and stay painted.
    private static bool IsOnPage(RoleSpec spec)
    {
        return spec.Background == ThemeColor.BaseBackground;
    }

    // The selection of a pane without focus: the theme's selection tint moved halfway to the page, so it stays a
    // visible but quiet step (ux-look.md, section 4). Derived, so theme files need no new colour.
    private static Rgb DimSelection(Theme theme)
    {
        return Rgb.Blend(theme.Colors[ThemeColor.Selection], theme.Colors[ThemeColor.BaseBackground], 0.5);
    }

    private static Rgb BackgroundOf(Theme theme, PaletteRole role, RoleSpec spec)
    {
        return role switch
        {
            PaletteRole.SelectedDim => DimSelection(theme),
            PaletteRole.Range => RangeSelection(theme),
            _ => theme.Colors[spec.Background],
        };
    }

    // The text of a range row: the theme's text moved towards black or white, so the tint can go further from the
    // page and the text still reads (many selection tints sit at the contrast limit of the plain text).
    private static Rgb RangeText(Theme theme)
    {
        var extreme = theme.Variant == ThemeVariant.Light ? new Rgb(0, 0, 0) : new Rgb(255, 255, 255);

        return Rgb.Blend(theme.Colors[ThemeColor.BaseForeground], extreme, RangeTextShare);
    }

    // The Visual range: the selection tint moved towards the accent, so a range reads stronger than a lone selected
    // row (ux-look.md, section 4). Derived, so theme files need no new colour. The step shrinks until the text keeps
    // its contrast (4.5, or 7 in a theme that reaches it on the selection).
    private static Rgb RangeSelection(Theme theme)
    {
        var selection = theme.Colors[ThemeColor.Selection];
        var accent = theme.Colors[ThemeColor.Accent];
        var text = RangeText(theme);
        var required = Contrast.Ratio(theme.Colors[ThemeColor.BaseForeground], selection) >= HighContrast ? HighContrast : TextContrast;

        for (var share = RangeAccentShare; share > 0; share -= RangeAccentStep)
        {
            var tint = Rgb.Blend(selection, accent, share);

            if (Contrast.Ratio(text, tint) >= required)
            {
                return tint;
            }
        }

        return selection;
    }

    // A filled chip: the page colour in bold on the role's own colour. Bold only (no reverse): the bars are reversed
    // without colour, so a chip that is not reverses back to normal and stands out of the bar.
    private static RoleSpec Chip(ThemeColor fill)
    {
        return new RoleSpec(ThemeColor.BaseBackground, fill, RoleStyle.Bold, RoleGround.Chip);
    }

    private static Ansi16Color Foreground16(Theme theme, RoleSpec spec)
    {
        if (spec.Ground == RoleGround.Chip)
        {
            return theme.Variant == ThemeVariant.Light ? Ansi16Color.White : Ansi16Color.Black;
        }

        if (spec.Ground == RoleGround.OnBlue)
        {
            return OnBlueForeground(theme, spec);
        }

        if (spec.Ground == RoleGround.Range)
        {
            return theme.Variant == ThemeVariant.Light ? Ansi16Color.White : Ansi16Color.Black;
        }

        var light = theme.Variant == ThemeVariant.Light;

        if (spec.Foreground == ThemeColor.BaseForeground)
        {
            return BaseForeground16(light, spec.Ground == RoleGround.Strong);
        }

        if (theme.Ansi.TryGetValue(spec.Foreground, out var chosen))
        {
            return chosen;
        }

        return light ? LightDefault(spec.Foreground) : DarkDefault(spec.Foreground);
    }

    // The key of a hint chip must differ from its label on the same blue bar (#676). The light 16-colour table has no
    // bright colour that reads on blue (bright yellow is dark gold there, contrast 3.0), so a light theme keeps the
    // white bold key and white label and is told apart by bold only (follow-up in the PR).
    private static Ansi16Color OnBlueForeground(Theme theme, RoleSpec spec)
    {
        var dark = theme.Variant != ThemeVariant.Light;

        return spec.Foreground == ThemeColor.Warn && dark ? Ansi16Color.BrightYellow : Ansi16Color.White;
    }

    private static Ansi16Color Background16(Theme theme, RoleSpec spec)
    {
        var variant = theme.Variant;

        if (spec.Ground == RoleGround.Chip)
        {
            return theme.Ansi.TryGetValue(spec.Background, out var fill)
                ? fill
                : variant == ThemeVariant.Light ? LightDefault(spec.Background) : DarkDefault(spec.Background);
        }

        if (spec.Ground == RoleGround.OnBlue)
        {
            return Ansi16Color.Blue;
        }

        if (spec.Ground == RoleGround.Range)
        {
            return variant == ThemeVariant.Light ? Ansi16Color.Black : Ansi16Color.Gray;
        }

        return variant == ThemeVariant.Light ? Ansi16Color.White : Ansi16Color.Black;
    }

    private static Ansi16Color BaseForeground16(bool light, bool strong)
    {
        if (light)
        {
            return Ansi16Color.Black;
        }

        return strong ? Ansi16Color.White : Ansi16Color.Gray;
    }

    // Bright accents on a dark terminal. BrightBlue is left out: the xterm value does not reach 4.5 on black.
    private static Ansi16Color DarkDefault(ThemeColor color)
    {
        return color switch
        {
            ThemeColor.Accent => Ansi16Color.BrightCyan,
            ThemeColor.Good => Ansi16Color.BrightGreen,
            ThemeColor.Bad => Ansi16Color.BrightRed,
            ThemeColor.Remote => Ansi16Color.BrightMagenta,
            ThemeColor.Warn => Ansi16Color.BrightYellow,
            ThemeColor.Info => Ansi16Color.Cyan,
            _ => Ansi16Color.DarkGray,
        };
    }

    // Normal-intensity accents on a light terminal, which darkens them.
    private static Ansi16Color LightDefault(ThemeColor color)
    {
        return color switch
        {
            ThemeColor.Accent => Ansi16Color.Blue,
            ThemeColor.Good => Ansi16Color.Green,
            ThemeColor.Bad => Ansi16Color.Red,
            ThemeColor.Remote => Ansi16Color.Magenta,
            ThemeColor.Warn => Ansi16Color.Yellow,
            ThemeColor.Info => Ansi16Color.Cyan,
            _ => Ansi16Color.DarkGray,
        };
    }
}
