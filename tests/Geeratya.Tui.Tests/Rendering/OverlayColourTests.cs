using Geeratya.Core.Text;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;
using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>The overlay surface, the dimmed view and the overlay roles in every colour tier (<c>docs/ux-look.md</c>, section 6).</summary>
[Trait("Os", "Independent")]
public class OverlayColourTests
{
    private const double SurfaceContrast = 1.3;
    // Body text keeps 4.5 on the surface; muted and accent-coloured text keep 3.0 (a raised fill costs them a little, as on the selection tint).
    private const double MutedDimContrast = 3.0;
    private const double FrameContrast = 3.0;
    private const double TextContrast = 4.5;

    public static TheoryData<string> Themes => [.. BuiltInThemes.Names];

    [Theory]
    [MemberData(nameof(Themes))]
    public void GivenATheme_WhenResolvingTheSurface_ThenItIsRaisedFromThePageAndTextReadsOnIt(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;
        var selection = Contrast.Ratio(theme.Colors[ThemeColor.Selection], theme.PageBackground);

        Assert.True(
            Contrast.Ratio(theme.Surface, theme.PageBackground) >= Math.Min(SurfaceContrast, selection),
            $"{name}: the surface is not raised from the page");

        foreach (var role in new[] { PaletteRole.Body, PaletteRole.Muted, PaletteRole.Key, PaletteRole.Heading })
        {
            var style = Palette.Resolve(theme, ColorSupport.TrueColor, role, SpanGround.Surface, true);

            Assert.True(
                Contrast.Ratio(style.Foreground!.Value, style.Background!.Value) >= (role == PaletteRole.Body ? TextContrast : MutedDimContrast),
                $"{name}: {role} on the surface");
        }
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void GivenATheme_WhenDimmingTheView_ThenMutedTextKeepsContrastThreeAndNoTextGetsDarkerThanThat(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;
        var muted = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.Muted, SpanGround.Dim, true);
        var body = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.Body, SpanGround.Dim, true);

        Assert.InRange(theme.DimAmount, 0, 0.3);
        Assert.True(Contrast.Ratio(muted.Foreground!.Value, muted.Background!.Value) >= MutedDimContrast, name);
        Assert.NotEqual(Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.Body).Foreground, body.Foreground);
        Assert.Equal(Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.Body).Background, body.Background);
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void GivenATheme_WhenResolvingTheOverlayRoles_ThenFramesAndChipsKeepTheirContrastInBothColourTiers(string name)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;
        PaletteRole[] frames = [PaletteRole.OverlayFrame, PaletteRole.OverlayFrameWarn, PaletteRole.OverlayFrameBad];
        PaletteRole[] chips = [PaletteRole.OverlayChip, PaletteRole.OverlayChipWarn, PaletteRole.OverlayChipBad];

        foreach (var tier in new[] { ColorSupport.TrueColor, ColorSupport.Basic16 })
        {
            foreach (var role in frames)
            {
                var style = Palette.Resolve(theme, tier, role, SpanGround.Surface, true);

                Assert.True(
                    Contrast.Ratio(style.Foreground!.Value, style.Background!.Value) >= FrameContrast,
                    $"{name} {tier}: {role} on the surface");
                Assert.True(style.Style.HasFlag(RoleStyle.Bold));
            }

            foreach (var role in chips)
            {
                var style = Palette.Resolve(theme, tier, role);

                Assert.True(
                    Contrast.Ratio(style.Foreground!.Value, style.Background!.Value) >= TextContrast,
                    $"{name} {tier}: {role}");
            }
        }
    }

    [Fact]
    public void GivenNoColour_WhenResolvingTheOverlayRoles_ThenTheFrameIsBoldTheChipReversedAndTheViewUnchanged()
    {
        var theme = BuiltInThemes.Default;

        Assert.Equal(RoleStyle.Bold, Palette.Resolve(theme, ColorSupport.None, PaletteRole.OverlayFrame).Style);
        Assert.Equal(
            RoleStyle.Bold | RoleStyle.Reverse,
            Palette.Resolve(theme, ColorSupport.None, PaletteRole.OverlayChip).Style);
        Assert.Equal(
            Palette.Resolve(theme, ColorSupport.None, PaletteRole.Body),
            Palette.Resolve(theme, ColorSupport.None, PaletteRole.Body, SpanGround.Dim | SpanGround.Surface, true));
    }

    [Fact]
    public void GivenSixteenColours_WhenDimmingTheView_ThenItIsFaintOrBrightBlackOnAClassicConsole()
    {
        var theme = BuiltInThemes.Default;
        var plain = Palette.Resolve(theme, ColorSupport.Basic16, PaletteRole.Body);
        var faint = Palette.Resolve(theme, ColorSupport.Basic16, PaletteRole.Body, SpanGround.Dim, true);
        var classic = Palette.Resolve(theme, ColorSupport.Basic16, PaletteRole.Body, SpanGround.Dim, false);
        var selected = Palette.Resolve(theme, ColorSupport.Basic16, PaletteRole.Selected, SpanGround.Dim, true);

        Assert.True(faint.Style.HasFlag(RoleStyle.Faint));
        Assert.Equal(plain.Foreground16, faint.Foreground16);
        Assert.False(classic.Style.HasFlag(RoleStyle.Faint));
        Assert.Equal(Ansi16Color.DarkGray, classic.Foreground16);
        Assert.Equal(Palette.Resolve(theme, ColorSupport.Basic16, PaletteRole.Selected), selected);
    }

    [Fact]
    public void GivenAFilledChip_WhenDimmingOrRaisingIt_ThenItKeepsItsFill()
    {
        var theme = BuiltInThemes.Default;
        var chip = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.OverlayChip);

        Assert.Equal(chip.Background, Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.OverlayChip, SpanGround.Surface | SpanGround.Dim, true).Background);
    }

    [Fact]
    public void GivenTheOverlayTextRoles_WhenMappingToPalette_ThenEachHasItsOwnRole()
    {
        Assert.Equal(PaletteRole.OverlayFrame, PaletteTable.For(TextRole.OverlayFrame));
        Assert.Equal(PaletteRole.OverlayFrameWarn, PaletteTable.For(TextRole.OverlayFrameWarn));
        Assert.Equal(PaletteRole.OverlayFrameBad, PaletteTable.For(TextRole.OverlayFrameBad));
        Assert.Equal(PaletteRole.OverlayChip, PaletteTable.For(TextRole.OverlayChip));
        Assert.Equal(PaletteRole.OverlayChipWarn, PaletteTable.For(TextRole.OverlayChipWarn));
        Assert.Equal(PaletteRole.OverlayChipBad, PaletteTable.For(TextRole.OverlayChipBad));
    }

    [Fact]
    public void GivenABoxOverARow_WhenCompositing_ThenTheRowKeepsItsCellsAroundTheBoxAndNeverHalfAWideCharacter()
    {
        var under = new StyledLine([new StyledSpan("ab日本語cdefghij", TextRole.Body, SpanGround.Dim)]);
        var box = OverlayBox.Wall(StyledLine.Of("x"), 6);

        // The box covers columns 3 to 10: the wide character at columns 2 and 3 is cut and becomes a space.
        var row = OverlayBox.Over(under, box, 3, 15);

        Assert.Equal("ab │ x    │fghi", row.PlainText);
        Assert.All(
            row.Spans.Where(span => span.Text.Contains('│')),
            span => Assert.Equal(SpanGround.Surface, span.Ground));
        Assert.Equal(SpanGround.Dim, row.Spans[^1].Ground);
    }

    [Fact]
    public void GivenALongTitle_WhenBuildingTheTopEdge_ThenTheChipIsCutToTheBoxWidth()
    {
        var edge = OverlayBox.Top(12, "A very long title indeed");

        Assert.Equal(12, TextWidth.Of(edge.PlainText));
        Assert.Equal('╭', edge.PlainText[0]);
        Assert.Equal('╮', edge.PlainText[^1]);
    }
}
