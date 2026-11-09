using Geeratya.Core.Text;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;
using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public sealed class ChipCapsTests
{
    [Fact]
    public void GivenALabelChip_WhenApplyingCaps_ThenThePaddingCellsBecomeCapsInTheCapRoleAndTheWidthIsKept()
    {
        var line = new StyledLine([new StyledSpan(" alpha ", TextRole.LabelChip2)]);

        var rounded = ChipCaps.Apply(line);

        Assert.Equal(
            [
                new StyledSpan(ChipCaps.Left, TextRole.LabelCap2),
                new StyledSpan("alpha", TextRole.LabelChip2),
                new StyledSpan(ChipCaps.Right, TextRole.LabelCap2),
            ],
            rounded.Spans);
        Assert.Equal(TextWidth.Of(line.PlainText), TextWidth.Of(rounded.PlainText));
    }

    [Fact]
    public void GivenOtherSpans_WhenApplyingCaps_ThenTheyAreLeftAlone()
    {
        var line = new StyledLine(
        [
            new StyledSpan(" 1 Board ", TextRole.TabActive),
            new StyledSpan("[--]", TextRole.UnassignedChip),
            new StyledSpan("  ", TextRole.LabelChip0),
        ]);

        Assert.Same(line, ChipCaps.Apply(line));
    }

    [Fact]
    public void GivenAChipOnASelectedRow_WhenApplyingCaps_ThenEachCapTakesTheGroundOfItsNeighbour()
    {
        var line = new StyledLine(
        [
            new StyledSpan(" ", TextRole.Body, SpanGround.Selection),
            new StyledSpan(" beta ", TextRole.LabelChip1),
            new StyledSpan(" ", TextRole.Body, SpanGround.Selection | SpanGround.Dim),
        ]);

        var spans = ChipCaps.Apply(line).Spans;

        Assert.Equal(SpanGround.Selection, spans[1].Ground);
        Assert.Equal(SpanGround.Page, spans[2].Ground);
        Assert.Equal(SpanGround.Selection | SpanGround.Dim, spans[3].Ground);
    }

    [Fact]
    public void GivenAFrame_WhenApplyingCaps_ThenHeaderAndBodyChipsAreRoundedAndTheStatusLineIsKept()
    {
        var chip = new StyledLine([new StyledSpan(" x ", TextRole.LabelChip0)]);
        var frame = new StyledFrame([chip], [chip], "status");

        var rounded = ChipCaps.Apply(frame);

        Assert.Equal($"{ChipCaps.Left}x{ChipCaps.Right}", rounded.Header[0].PlainText);
        Assert.Equal($"{ChipCaps.Left}x{ChipCaps.Right}", rounded.Body[0].PlainText);
        Assert.Equal("status", rounded.StatusLine);
    }

    [Theory]
    [InlineData(TextRole.LabelChip0, PaletteRole.LabelChip0, PaletteRole.LabelCap0)]
    [InlineData(TextRole.LabelChip4, PaletteRole.LabelChip4, PaletteRole.LabelCap4)]
    public void GivenAChipRole_WhenResolvingItsCap_ThenTheCapIsTheChipFillOnThePageInEveryColourTier(
        TextRole chipText,
        PaletteRole chip,
        PaletteRole cap)
    {
        var theme = BuiltInThemes.Default;

        Assert.Equal(cap, PaletteTable.For(ChipCaps.CapRole(chipText)!.Value));

        foreach (var tier in new[] { ColorSupport.TrueColor, ColorSupport.Basic16 })
        {
            var fill = Palette.Resolve(theme, tier, chip);
            var rounded = Palette.Resolve(theme, tier, cap);
            var page = Palette.Resolve(theme, tier, PaletteRole.Body);

            Assert.Equal(fill.Background, rounded.Foreground);
            Assert.Equal(fill.Background16, rounded.Foreground16);
            Assert.Equal(page.Background, rounded.Background);
        }
    }

    [Fact]
    public void GivenEveryLabelChipRole_WhenAskingForACap_ThenThereIsOne()
    {
        foreach (var role in new[] { TextRole.LabelChip0, TextRole.LabelChip1, TextRole.LabelChip2, TextRole.LabelChip3, TextRole.LabelChip4 })
        {
            Assert.NotNull(ChipCaps.CapRole(role));
        }

        Assert.Null(ChipCaps.CapRole(TextRole.Body));
    }

    [Fact]
    public void GivenACapOnADimmedOrSelectedGround_WhenResolving_ThenItKeepsTheChipFillInEveryBuiltInTheme()
    {
        foreach (var name in BuiltInThemes.Names)
        {
            var theme = new ThemeSwitcher(new ThemeCatalog(null)).Start(name, false).Theme;

            foreach (var ground in new[] { SpanGround.Dim, SpanGround.Selection, SpanGround.Selection | SpanGround.Dim })
            {
                var fill = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.LabelChip3).Background;
                var cap = Palette.Resolve(theme, ColorSupport.TrueColor, PaletteRole.LabelCap3, ground, true);

                Assert.Equal(fill, cap.Foreground);
            }
        }
    }
}
