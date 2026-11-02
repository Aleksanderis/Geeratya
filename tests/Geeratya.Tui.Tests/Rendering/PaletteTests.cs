using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;
using Terminal.Gui.Drawing;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class PaletteTests
{
    public static TheoryData<string, ColorSupport> ThemesAndTiers
    {
        get
        {
            var data = new TheoryData<string, ColorSupport>();

            foreach (var theme in Themes.All)
            {
                data.Add(theme.Name, ColorSupport.TrueColor);
                data.Add(theme.Name, ColorSupport.Basic16);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ThemesAndTiers))]
    public void GivenTheme_WhenResolvingEveryRole_ThenContrastMeetsTheRequiredRatio(string name, ColorSupport tier)
    {
        var theme = Themes.Find(name)!;

        foreach (var role in PaletteTable.Roles)
        {
            var style = Palette.Resolve(theme, tier, role);

            var ratio = Contrast.Ratio(style.Foreground!.Value, style.Background!.Value);

            Assert.True(
                ratio >= PaletteTable.RequiredContrast(role),
                $"{role} in {name}/{tier} has contrast {ratio:F2}");
        }
    }

    [Fact]
    public void GivenEveryBuiltInTheme_WhenListingRoles_ThenEveryRoleIsDefined()
    {
        foreach (var theme in Themes.All)
        {
            foreach (var role in PaletteTable.Roles)
            {
                Assert.True(theme.Entries.ContainsKey(role), $"{role} missing in {theme.Name}");
            }

            Assert.Equal(PaletteTable.Roles.Count, theme.Entries.Count);
        }
    }

    [Fact]
    public void GivenHighContrastTheme_WhenResolvingTextRoles_ThenContrastIsAtLeastSevenInTrueColour()
    {
        foreach (var role in PaletteTable.Roles.Where(role => role != PaletteRole.Divider))
        {
            var style = Palette.Resolve(Themes.HighContrast, ColorSupport.TrueColor, role);

            Assert.True(Contrast.Ratio(style.Foreground!.Value, style.Background!.Value) >= 7.0, $"{role}");
        }
    }

    [Fact]
    public void GivenBuiltInThemes_WhenComparingBodyAndSelectedWithoutColour_ThenSelectionIsReversed()
    {
        foreach (var theme in Themes.All)
        {
            var body = Palette.Resolve(theme, ColorSupport.None, PaletteRole.Body).Style;
            var selected = Palette.Resolve(theme, ColorSupport.None, PaletteRole.Selected).Style;

            Assert.NotEqual(body, selected);
            Assert.True(selected.HasFlag(RoleStyle.Reverse), theme.Name);
        }
    }

    [Fact]
    public void GivenDarkAndLightThemes_WhenResolvingBody_ThenBackgroundsDiffer()
    {
        Assert.NotEqual(
            Palette.Resolve(Themes.Dark, ColorSupport.TrueColor, PaletteRole.Body).Background,
            Palette.Resolve(Themes.Light, ColorSupport.TrueColor, PaletteRole.Body).Background);
    }

    [Fact]
    public void GivenNoColourTier_WhenResolvingEveryRole_ThenNoRgbColoursAreUsed()
    {
        foreach (var role in PaletteTable.Roles)
        {
            var style = Palette.Resolve(ColorSupport.None, role);

            Assert.Null(style.Foreground);
            Assert.Null(style.Background);
        }
    }

    [Fact]
    public void GivenNoColourTier_WhenComparingRoles_ThenStatusAndNetworkStatesDifferFromBodyWithoutColour()
    {
        var body = Palette.Resolve(ColorSupport.None, PaletteRole.Body).Style;
        PaletteRole[] distinct =
        [
            PaletteRole.Status,
            PaletteRole.Error,
            PaletteRole.Loading,
            PaletteRole.Offline,
            PaletteRole.RateLimited,
            PaletteRole.Stale,
        ];

        foreach (var role in distinct)
        {
            Assert.NotEqual(body, Palette.Resolve(ColorSupport.None, role).Style);
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

    [Theory]
    [InlineData(ColorSupport.TrueColor)]
    [InlineData(ColorSupport.Basic16)]
    [InlineData(ColorSupport.None)]
    public void GivenAnyTier_WhenResolvingEveryRole_ThenForegroundDiffersFromBackground(ColorSupport tier)
    {
        foreach (var role in PaletteTable.Roles)
        {
            var style = Palette.Resolve(tier, role);

            var foreground = style.Foreground ?? Ansi16.ToRgb(style.Foreground16!.Value);
            var background = style.Background ?? Ansi16.ToRgb(style.Background16!.Value);

            Assert.NotEqual(foreground, background);
        }
    }
}
