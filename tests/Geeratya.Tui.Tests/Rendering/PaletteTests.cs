using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;
using Terminal.Gui.Drawing;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class PaletteTests
{
    public static TheoryData<ColorSupport> ColorTiers => [ColorSupport.TrueColor, ColorSupport.Basic16];

    [Theory]
    [MemberData(nameof(ColorTiers))]
    public void GivenColourTier_WhenResolvingEveryRole_ThenContrastMeetsTheRequiredRatio(ColorSupport tier)
    {
        foreach (var role in PaletteTable.Roles)
        {
            var style = Palette.Resolve(tier, role);

            var ratio = Contrast.Ratio(style.Foreground!.Value, style.Background!.Value);

            Assert.True(
                ratio >= PaletteTable.RequiredContrast(role),
                $"{role} in {tier} has contrast {ratio:F2}");
        }
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
