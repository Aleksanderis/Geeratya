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

    public static TheoryData<ColorSupport> ColorTiers => [ColorSupport.TrueColor, ColorSupport.Basic16];

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

    [Fact]
    public void GivenAnyTheme_WhenResolvingWarningAndKey_ThenWarningIsTheWarnColour()
    {
        foreach (var name in BuiltInThemes.Names)
        {
            var theme = BuiltInThemes.Find(name)!.Theme!;

            Assert.Equal(
                Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.Key).Foreground,
                Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.Warning).Foreground);
        }

        Assert.Equal(PaletteRole.Warning, PaletteTable.For(TextRole.Warning));
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
