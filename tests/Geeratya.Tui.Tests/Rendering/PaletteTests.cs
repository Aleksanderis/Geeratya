using Geeratya.Core.Text;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;
using Geeratya.Tui.Rendering.Themes;
using Terminal.Gui.Drawing;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class PaletteTests
{
    // Empirical floor, not a WCAG rule: the faintest shipped bar is about 1.27.
    private const double MinimumSelectionBarRatio = 1.2;

    public static TheoryData<string, ColorSupport> ThemesAndTiers
    {
        get
        {
            var data = new TheoryData<string, ColorSupport>();

            foreach (var name in BuiltInThemes.Names)
            {
                data.Add(name, ColorSupport.TrueColor);
                data.Add(name, ColorSupport.Basic16);
            }

            return data;
        }
    }

    private static readonly PaletteRole[] _labelRoles =
    [
        PaletteRole.LabelChip0, PaletteRole.LabelChip1, PaletteRole.LabelChip2, PaletteRole.LabelChip3, PaletteRole.LabelChip4,
    ];

    public static TheoryData<string> ThemeNames => [.. BuiltInThemes.Names];

    public static TheoryData<ColorSupport> ColorTiers => [ColorSupport.TrueColor, ColorSupport.Basic16];

    [Theory]
    [InlineData(PaletteRole.LabelChip0)]
    [InlineData(PaletteRole.LabelChip3)]
    public void GivenNoColour_WhenResolvingALabelChip_ThenItIsReversedAndUnderlinedOnlyThere(PaletteRole role)
    {
        var theme = BuiltInThemes.Default;
        var plain = Palette.Resolve(theme, ColorSupport.None, role).Style;
        var selected = Palette.Resolve(theme, ColorSupport.None, PaletteRole.Selected).Style;

        Assert.True(plain.HasFlag(RoleStyle.Reverse | RoleStyle.Underline));
        Assert.NotEqual(selected, plain);

        foreach (var tier in new[] { ColorSupport.TrueColor, ColorSupport.Basic16 })
        {
            var style = Palette.Resolve(theme, tier, role).Style;

            Assert.False(style.HasFlag(RoleStyle.Reverse));
            Assert.False(style.HasFlag(RoleStyle.Underline));
        }
    }

    [Theory]
    [MemberData(nameof(ThemesAndTiers))]
    public void GivenBuiltInThemeAndTier_WhenResolvingEveryRole_ThenContrastMeetsTheRequiredRatio(
        string name,
        ColorSupport tier)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;

        foreach (var role in PaletteTable.Roles)
        {
            var style = Palette.Resolve(theme, tier, role);

            var ratio = Contrast.Ratio(style.Foreground!.Value, style.Background!.Value);

            Assert.True(
                ratio >= PaletteTable.RequiredContrast(role),
                $"{name}: {role} in {tier} has contrast {ratio:F2}");
        }
    }

    [Theory]
    [MemberData(nameof(ThemesAndTiers))]
    public void GivenBuiltInThemeAndTier_WhenResolvingHintKeyAndLabel_ThenForegroundsDiffer(
        string name,
        ColorSupport tier)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;

        var key = Palette.Resolve(theme, tier, PaletteRole.BarKey);
        var label = Palette.Resolve(theme, tier, PaletteRole.BarMuted);

        Assert.NotEqual(label.Foreground, key.Foreground);
    }

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void GivenBuiltInThemeAtTrueColour_WhenResolvingTheBar_ThenKeysAreAccentAndTextIsMutedLikeGitunia(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;

        var key = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.BarKey);
        var text = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.Status);

        var button = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.BarButton);

        Assert.Equal(theme.Colors[ThemeColor.Accent], key.Foreground);
        Assert.Equal(theme.Colors[ThemeColor.Accent], button.Foreground);
        Assert.True(button.Style.HasFlag(RoleStyle.Bold));
        Assert.Equal(theme.Colors[ThemeColor.Muted], text.Foreground);
        Assert.Equal(theme.Colors[ThemeColor.BarBackground], key.Background);
    }

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void GivenBuiltInThemeAtTrueColour_WhenResolvingAnInactiveTab_ThenItIsAButtonApartFromTheBar(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;

        var tab = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.TabInactive);
        var bar = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.Status);

        Assert.NotEqual(bar.Background, tab.Background);
    }

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void GivenBuiltInThemeAtSixteenColours_WhenResolvingHintKey_ThenItIsTheAccentOnTheUnfilledBar(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;
        var key = Palette.Resolve(theme, ColorSupport.Basic16, PaletteRole.BarKey);
        var expectedBar = theme.Variant == ThemeVariant.Light ? Ansi16Color.White : Ansi16Color.Black;
        var page = Palette.Resolve(theme, ColorSupport.Basic16, PaletteRole.Body);

        var button = Palette.Resolve(theme, ColorSupport.Basic16, PaletteRole.BarButton);

        Assert.Equal(Palette.Resolve(theme, ColorSupport.Basic16, PaletteRole.Heading).Foreground16, key.Foreground16);
        Assert.True(key.Style.HasFlag(RoleStyle.Bold));
        Assert.Equal(key.Foreground16, button.Foreground16);
        Assert.NotEqual(Ansi16Color.BrightYellow, key.Foreground16);
        Assert.Equal(expectedBar, key.Background16);
        Assert.Equal(page.Background16, key.Background16);
        Assert.Equal(page.Background16, Palette.Resolve(theme, ColorSupport.Basic16, PaletteRole.Status).Background16);
    }

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void GivenBuiltInThemeAtSixteenColours_WhenDimmingTheBar_ThenItDimsLikeThePage(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;

        var key = Palette.Resolve(theme, ColorSupport.Basic16, PaletteRole.BarKey, SpanGround.Dim, true);
        var page = Palette.Resolve(theme, ColorSupport.Basic16, PaletteRole.Body, SpanGround.Dim, true);

        Assert.Equal(page.Style.HasFlag(RoleStyle.Faint), key.Style.HasFlag(RoleStyle.Faint));
    }

    [Fact]
    public void GivenHighContrastTheme_WhenResolvingEveryRoleAtTrueColour_ThenTextReachesSevenToOne()
    {
        var theme = BuiltInThemes.Find("high-contrast")!.Theme!;

        foreach (var role in PaletteTable.Roles)
        {
            var style = Palette.Resolve(theme, ColorSupport.TrueColor, role);

            var ratio = Contrast.Ratio(style.Foreground!.Value, style.Background!.Value);

            Assert.True(ratio >= 7.0, $"{role} has contrast {ratio:F2}");
        }
    }

    [Theory]
    [MemberData(nameof(ThemesAndTiers))]
    public void GivenBuiltInThemeAndTier_WhenResolvingEveryRole_ThenForegroundDiffersFromBackground(
        string name,
        ColorSupport tier)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;

        foreach (var role in PaletteTable.Roles)
        {
            var style = Palette.Resolve(theme, tier, role);

            Assert.NotEqual(style.Foreground, style.Background);
        }
    }

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void GivenAnyTheme_WhenResolvingTheEditFieldAndCaret_ThenEveryTierSetsThemApartFromThePage(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;
        var page = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.Body);
        var field = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.EditField);
        var caret = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.EditCaret);

        Assert.NotEqual(page.Background, field.Background);
        Assert.NotEqual(field.Background, caret.Background);
        Assert.Equal(Ansi16Color.Blue, Palette.Resolve(theme, ColorSupport.Basic16, PaletteRole.EditField).Background16);
        Assert.NotEqual(Ansi16Color.Blue, Palette.Resolve(theme, ColorSupport.Basic16, PaletteRole.EditCaret).Background16);

        foreach (var tier in new[] { ColorSupport.TrueColor, ColorSupport.Basic16 })
        {
            var style = Palette.Resolve(theme, tier, PaletteRole.EditField).Style;

            Assert.False(style.HasFlag(RoleStyle.Underline) || style.HasFlag(RoleStyle.Reverse));
        }

        var plainField = Palette.Resolve(theme, ColorSupport.None, PaletteRole.EditField).Style;
        var plainCaret = Palette.Resolve(theme, ColorSupport.None, PaletteRole.EditCaret).Style;

        Assert.True(plainField.HasFlag(RoleStyle.Reverse));
        Assert.False(plainCaret.HasFlag(RoleStyle.Reverse));
    }

    [Fact]
    public void GivenDarkAndLightVariants_WhenResolvingBodyAtSixteenColours_ThenTheVariantPicksBlackOrWhiteGround()
    {
        var dark = Palette.Resolve(BuiltInThemes.Default, ColorSupport.Basic16, PaletteRole.Body);
        var light = Palette.Resolve(
            BuiltInThemes.Find(BuiltInThemes.DefaultLightName)!.Theme!,
            ColorSupport.Basic16,
            PaletteRole.Body);

        Assert.Equal(Ansi16Color.Black, dark.Background16);
        Assert.Equal(Ansi16Color.White, light.Background16);
        Assert.Equal(Ansi16Color.Black, light.Foreground16);
    }

    [Fact]
    public void GivenAnsiOverride_WhenResolvingAtSixteenColours_ThenOnlyThatColourChanges()
    {
        var theme = ThemeFileParser.Parse("mine", MochaFile(string.Empty)).Theme!;
        var overridden = ThemeFileParser.Parse("mine", MochaFile("\"ansi\": { \"accent\": \"yellow\" },")).Theme!;

        Assert.Equal(
            Ansi16Color.Yellow,
            Palette.Resolve(overridden, ColorSupport.Basic16, PaletteRole.Heading).Foreground16);
        Assert.NotEqual(
            Ansi16Color.Yellow,
            Palette.Resolve(theme, ColorSupport.Basic16, PaletteRole.Heading).Foreground16);
        Assert.Equal(
            Palette.Resolve(theme, ColorSupport.Basic16, PaletteRole.Error),
            Palette.Resolve(overridden, ColorSupport.Basic16, PaletteRole.Error));
    }

    [Fact]
    public void GivenNoColourTier_WhenResolvingEveryRole_ThenNoRgbColoursAreUsed()
    {
        foreach (var role in PaletteTable.Roles)
        {
            var style = Palette.Resolve(BuiltInThemes.Default, ColorSupport.None, role);

            Assert.Null(style.Foreground);
            Assert.Null(style.Background);
        }
    }

    [Fact]
    public void GivenNoColourTier_WhenComparingRoles_ThenStatusAndNetworkStatesDifferFromBodyWithoutColour()
    {
        var body = Palette.Resolve(BuiltInThemes.Default, ColorSupport.None, PaletteRole.Body).Style;
        PaletteRole[] distinct =
        [
            PaletteRole.Status,
            PaletteRole.Error,
            PaletteRole.Loading,
            PaletteRole.Offline,
            PaletteRole.RateLimited,
            PaletteRole.Warning,
            PaletteRole.Success,
            PaletteRole.SuccessChip,
            PaletteRole.Stale,
        ];

        foreach (var role in distinct)
        {
            Assert.NotEqual(body, Palette.Resolve(BuiltInThemes.Default, ColorSupport.None, role).Style);
        }
    }

    [Theory]
    [MemberData(nameof(ThemesAndTiers))]
    public void GivenBuiltInThemeAndTier_WhenResolvingSelectedLine_ThenItIsVisiblyDistinctFromBody(
        string name,
        ColorSupport tier)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;

        var body = Palette.Resolve(theme, tier, PaletteRole.Body);
        var selected = Palette.Resolve(theme, tier, PaletteRole.Selected);

        Assert.NotEqual(body.Background, selected.Background);
        Assert.True(
            Contrast.Ratio(body.Background!.Value, selected.Background!.Value) >= MinimumSelectionBarRatio,
            $"{name}: selection bar in {tier} is faint against the body");
    }

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void GivenAnyTheme_WhenResolvingKeyAndFieldLabel_ThenBothAreTheAccentColourNotWarnOrInfo(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;
        var accent = theme.Colors[ThemeColor.Accent];

        Assert.Equal(accent, Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.Key).Foreground);
        Assert.Equal(accent, Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.FieldLabel).Foreground);
        Assert.True(Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.Key).Style.HasFlag(RoleStyle.Bold));
        Assert.NotEqual(
            Palette.Resolve(theme, ColorSupport.Basic16, PaletteRole.Warning).Foreground16,
            Palette.Resolve(theme, ColorSupport.Basic16, PaletteRole.Key).Foreground16);
    }

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void GivenAnyTheme_WhenResolvingTheChipAndBlockRoles_ThenFillsAreDistinctAndOffThePage(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;
        var page = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.Body).Background;
        var selection = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.Selected).Background;
        var block = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.ValueBlock).Background;
        var unassigned = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.UnassignedChip);
        var bar = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.SelectionBar);
        var labels = _labelRoles;

        Assert.NotEqual(page, block);
        Assert.NotEqual(selection, block);
        Assert.Equal(page, unassigned.Background);
        Assert.Equal(theme.Colors[ThemeColor.Muted], unassigned.Foreground);
        Assert.Equal(theme.Colors[ThemeColor.Accent], bar.Foreground);
        Assert.Equal(labels.Length, labels.Select(role => Palette.Resolve(theme, ColorSupport.TrueColor, role).Background).Distinct().Count());
    }

    [Fact]
    public void GivenGlyphSets_WhenReadingPriorityDotAndDropdown_ThenUnicodeAndAsciiFormsExist()
    {
        var unicode = Geeratya.Tui.Rendering.Glyphs.For(GlyphSet.Unicode);
        var ascii = Geeratya.Tui.Rendering.Glyphs.For(GlyphSet.Ascii);

        Assert.Equal("▲=▼●▾", unicode.PriorityHigh + unicode.PriorityMedium + unicode.PriorityLow + unicode.StatusDot + unicode.Dropdown);
        Assert.Equal("^=v*v", ascii.PriorityHigh + ascii.PriorityMedium + ascii.PriorityLow + ascii.StatusDot + ascii.Dropdown);
    }

    [Fact]
    public void GivenNoColourTier_WhenResolvingSelectedLine_ThenItDiffersFromBodyWithoutColour()
    {
        foreach (var name in BuiltInThemes.Names)
        {
            var theme = BuiltInThemes.Find(name)!.Theme!;

            Assert.NotEqual(
                Palette.Resolve(theme, ColorSupport.None, PaletteRole.Body).Style,
                Palette.Resolve(theme, ColorSupport.None, PaletteRole.Selected).Style);
        }
    }

    [Fact]
    public void GivenAnsiColours_WhenCastToTerminalGuiNames_ThenTheNumbersMatch()
    {
        Assert.Equal((int)ColorName16.Black, (int)Ansi16Color.Black);
        Assert.Equal((int)ColorName16.DarkGray, (int)Ansi16Color.DarkGray);
        Assert.Equal((int)ColorName16.BrightYellow, (int)Ansi16Color.BrightYellow);
        Assert.Equal((int)ColorName16.White, (int)Ansi16Color.White);
    }

    [Fact]
    public void GivenBlackAndWhite_WhenMeasuringContrast_ThenRatioIsTwentyOne()
    {
        var ratio = Contrast.Ratio(new Rgb(0, 0, 0), new Rgb(255, 255, 255));

        Assert.Equal(21.0, ratio, 1);
    }

    private static string MochaFile(string extra)
    {
        return $$"""
            {
              "version": 1,
              "variant": "dark",
              {{extra}}
              "colors": {
                "accent": "#89b4fa", "good": "#a6e3a1", "bad": "#f38ba8", "remote": "#cba6f7",
                "warn": "#f9e2af", "info": "#89dceb", "muted": "#979db5", "border": "#6c7086",
                "barBackground": "#181825", "selection": "#45475a",
                "baseForeground": "#cdd6f4", "baseBackground": "#1e1e2e"
              }
            }
            """;
    }
}
