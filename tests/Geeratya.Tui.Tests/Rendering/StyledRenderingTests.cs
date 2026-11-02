using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;
using Geeratya.Tui.Tests.Fakes;
using Geeratya.Tui.Views;
using Terminal.Gui.Drawing;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class StyledRenderingTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IReadOnlyList<string> _helpLines = KeymapDocument.RenderHelpLines(DefaultKeymap.Load());

    [Theory]
    [InlineData(TextRole.Body, PaletteRole.Body)]
    [InlineData(TextRole.Muted, PaletteRole.Muted)]
    [InlineData(TextRole.Heading, PaletteRole.Heading)]
    [InlineData(TextRole.Strong, PaletteRole.Strong)]
    [InlineData(TextRole.Emphasis, PaletteRole.Emphasis)]
    [InlineData(TextRole.Code, PaletteRole.Code)]
    [InlineData(TextRole.Link, PaletteRole.Link)]
    [InlineData(TextRole.Quote, PaletteRole.Quote)]
    [InlineData(TextRole.Selected, PaletteRole.Selected)]
    [InlineData(TextRole.Key, PaletteRole.Key)]
    [InlineData(TextRole.StatusToDo, PaletteRole.StatusToDo)]
    [InlineData(TextRole.StatusInProgress, PaletteRole.StatusInProgress)]
    [InlineData(TextRole.StatusDone, PaletteRole.StatusDone)]
    [InlineData(TextRole.Error, PaletteRole.Error)]
    public void GivenTextRole_WhenMappingToPalette_ThenItIsTheExpectedPaletteRole(TextRole role, PaletteRole expected)
    {
        Assert.Equal(expected, PaletteTable.For(role));
    }

    [Theory]
    [InlineData(ColorSupport.TrueColor)]
    [InlineData(ColorSupport.Basic16)]
    [InlineData(ColorSupport.None)]
    public void GivenAnyTier_WhenResolvingTextStylingRoles_ThenEachDiffersFromBody(ColorSupport tier)
    {
        var body = Palette.Resolve(tier, PaletteRole.Body);
        PaletteRole[] roles =
        [
            PaletteRole.Heading,
            PaletteRole.Strong,
            PaletteRole.Emphasis,
            PaletteRole.Code,
            PaletteRole.Link,
            PaletteRole.Quote,
            PaletteRole.Selected,
            PaletteRole.Key,
            PaletteRole.StatusInProgress,
            PaletteRole.StatusDone,
        ];

        foreach (var role in roles)
        {
            Assert.NotEqual(body, Palette.Resolve(tier, role));
        }
    }

    [Theory]
    [InlineData(ColorSupport.TrueColor)]
    [InlineData(ColorSupport.Basic16)]
    public void GivenColourTier_WhenResolvingStrongEmphasisAndLink_ThenTheirAttributesAreKept(ColorSupport tier)
    {
        Assert.True(Palette.Resolve(tier, PaletteRole.Strong).Style.HasFlag(RoleStyle.Bold));
        Assert.True(Palette.Resolve(tier, PaletteRole.Emphasis).Style.HasFlag(RoleStyle.Italic));
        Assert.True(Palette.Resolve(tier, PaletteRole.Link).Style.HasFlag(RoleStyle.Underline));
        Assert.False(Palette.Resolve(tier, PaletteRole.Selected).Style.HasFlag(RoleStyle.Reverse));
    }

    [Fact]
    public void GivenSelectedAndDoneStyles_WhenCreatingAttributes_ThenTextStyleAndColoursCarryOver()
    {
        var selected = AttributeFactory.Create(Palette.Resolve(ColorSupport.None, PaletteRole.Selected));
        var done = AttributeFactory.Create(Palette.Resolve(ColorSupport.TrueColor, PaletteRole.StatusDone));

        Assert.True(selected.Style.HasFlag(TextStyle.Reverse));
        Assert.True(selected.Style.HasFlag(TextStyle.Bold));
        Assert.True(done.Style.HasFlag(TextStyle.Italic));
        Assert.Equal(new Color(0xA3, 0xBE, 0x8C), done.Foreground);
    }

    [Fact]
    public void GivenNoColourTier_WhenResolvingSelectedAndCategories_ThenAttributesDifferFromBody()
    {
        var body = Palette.Resolve(ColorSupport.None, PaletteRole.Body).Style;

        Assert.True(Palette.Resolve(ColorSupport.None, PaletteRole.Selected).Style.HasFlag(RoleStyle.Reverse));
        Assert.NotEqual(body, Palette.Resolve(ColorSupport.None, PaletteRole.StatusInProgress).Style);
        Assert.NotEqual(body, Palette.Resolve(ColorSupport.None, PaletteRole.StatusDone).Style);
        Assert.NotEqual(body, Palette.Resolve(ColorSupport.None, PaletteRole.Heading).Style);
        Assert.NotEqual(body, Palette.Resolve(ColorSupport.None, PaletteRole.Link).Style);
    }

    [Theory]
    [InlineData(ColorSupport.TrueColor)]
    [InlineData(ColorSupport.Basic16)]
    [InlineData(ColorSupport.None)]
    public void GivenStyledLine_WhenResolvingRuns_ThenEachSpanGetsItsRoleStyleInTheTier(ColorSupport tier)
    {
        var line = new StyledLine(
        [
            new StyledSpan("a ", TextRole.Body),
            new StyledSpan("b", TextRole.Selected),
            new StyledSpan(" c", TextRole.StatusDone),
        ]);

        var runs = StyledRuns.Resolve(line, tier);

        Assert.Equal(["a ", "b", " c"], runs.Select(run => run.Text));
        Assert.Equal(Palette.Resolve(tier, PaletteRole.Selected), runs[1].Style);
        Assert.Equal(Palette.Resolve(tier, PaletteRole.StatusDone), runs[2].Style);
    }

    [Fact]
    public void GivenWideCharacterAtTheEdge_WhenFitting_ThenItIsNotSplitAndWidthIsExact()
    {
        var line = new StyledLine([new StyledSpan("ab", TextRole.Body), new StyledSpan("中文", TextRole.Link)]);

        var fitted = StyledLines.Fit(line, 5);

        Assert.Equal("ab中 ", fitted.PlainText);
        Assert.Equal(5, TextWidth.Of(fitted.PlainText));
    }

    [Fact]
    public void GivenTestBodyRenderer_WhenBuilding_ThenItsStyledLinesAppearAndPlainTextDropsStyles()
    {
        var renderer = new FakeBodyRenderer();

        var frame = FrameBuilder.BuildStyled(AppState.Initial, 20, 5, _helpLines, _keymap, [renderer])!;

        Assert.Equal(TextRole.Selected, frame.Body[0].Spans[0].Role);
        Assert.StartsWith("> PROJ-1 To Do", frame.Body[0].PlainText);
        Assert.Equal(5, frame.PlainLines().Count);
        Assert.All(frame.PlainLines(), line => Assert.Equal(20, TextWidth.Of(line)));
        Assert.Contains(TextRole.StatusDone, frame.Body[1].Spans.Select(span => span.Role));
    }

    [Fact]
    public void GivenNoCustomRenderer_WhenBuilding_ThenHelpAndStartScreensAreChosenFromState()
    {
        var help = FrameBuilder.BuildStyled(AppState.Initial with { Mode = InputMode.Help }, 40, 10, _helpLines, _keymap)!;
        var start = FrameBuilder.BuildStyled(AppState.Initial, 40, 10, _helpLines, _keymap)!;

        Assert.StartsWith("Help", help.Body[0].PlainText);
        Assert.Contains(start.Body, line => line.PlainText.Contains("Geeratya"));
    }

    [Fact]
    public void GivenHeaderRow_WhenBuilding_ThenFrameKeepsItsHeightWithABlankHeaderLine()
    {
        var frame = FrameBuilder.BuildStyled(AppState.Initial, 30, 8, _helpLines, _keymap)!;

        Assert.Equal(FrameBuilder.HeaderRows, frame.Header.Count);
        Assert.Equal(8 - 1 - FrameBuilder.HeaderRows, frame.Body.Count);
        Assert.Equal(8, frame.PlainLines().Count);
        Assert.All(frame.Header, line => Assert.Equal(new string(' ', 30), line.PlainText));
    }
}
