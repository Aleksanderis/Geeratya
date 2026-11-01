using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class StyledRenderingTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly IReadOnlyList<string> _helpLines = KeymapDocument.RenderHelpLines(DefaultKeymap.Load());

    [Fact]
    public void GivenEveryTextRole_WhenMappingToPalette_ThenEachHasADistinctPaletteRole()
    {
        var mapped = Enum.GetValues<TextRole>().Select(PaletteTable.For).ToList();

        Assert.Equal(mapped.Count, mapped.Distinct().Count());
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
        var frame = FrameBuilder.BuildStyled(AppState.Initial, 30, 8, _helpLines, _keymap, null, 1)!;

        Assert.Single(frame.Header);
        Assert.Equal(6, frame.Body.Count);
        Assert.Equal(8, frame.PlainLines().Count);
        Assert.Equal(new string(' ', 30), frame.Header[0].PlainText);
    }

    private sealed class FakeBodyRenderer : IBodyRenderer
    {
        public bool AppliesTo(AppState state)
        {
            return true;
        }

        public IReadOnlyList<StyledLine> Render(AppState state, BodyContext context, int width, int height)
        {
            return
            [
                new StyledLine(
                [
                    new StyledSpan("> PROJ-1 ", TextRole.Selected),
                    new StyledSpan("To Do", TextRole.StatusToDo),
                ]),
                new StyledLine(
                [
                    new StyledSpan("  PROJ-2 ", TextRole.Muted),
                    new StyledSpan("Done", TextRole.StatusDone),
                ]),
            ];
        }
    }
}
