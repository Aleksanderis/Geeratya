using Geeratya.Core.Text;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class FrameGridTests
{
    private static readonly StyledSpan[] _noText = [];

    [Fact]
    public void GivenThreeColumnsSharingEdges_WhenDrawingRows_ThenOuterCornersAreRoundedAndJunctionsArePlain()
    {
        var grid = new FrameGrid(22, 4, [new PaneRect(0, 0, 4, 8), new PaneRect(0, 7, 4, 8), new PaneRect(0, 14, 4, 8)]);

        Assert.Equal("╭──────┬──────┬──────╮", Row(grid, 0));
        Assert.Equal("│      │      │      │", Row(grid, 1));
        Assert.Equal("╰──────┴──────┴──────╯", Row(grid, 3));
    }

    [Fact]
    public void GivenStackedPanes_WhenDrawingTheSharedEdge_ThenItIsOneLineWithSideJunctions()
    {
        var grid = new FrameGrid(10, 5, [new PaneRect(0, 0, 3, 10), new PaneRect(2, 0, 3, 10)]);

        Assert.Equal("├────────┤", Row(grid, 2));
        Assert.Equal("╭────────╮", Row(grid, 0));
        Assert.Equal("╰────────╯", Row(grid, 4));
    }

    [Fact]
    public void GivenAGridOfPanes_WhenTheLinesCross_ThenTheCrossGlyphIsUsed()
    {
        var grid = new FrameGrid(
            9,
            5,
            [new PaneRect(0, 0, 3, 5), new PaneRect(0, 4, 3, 5), new PaneRect(2, 0, 3, 5), new PaneRect(2, 4, 3, 5)]);

        Assert.Equal("├───┼───┤", Row(grid, 2));
    }

    [Fact]
    public void GivenAChip_WhenWrittenIntoTheTopEdge_ThenItReplacesBorderCellsAndKeepsTheWidth()
    {
        var pane = new PaneRect(0, 0, 3, 24);
        var grid = new FrameGrid(24, 3, [pane]);
        var chips = FrameGrid.Chips(pane, [new StyledSpan("To Do 5", TextRole.Heading)], [new StyledSpan("5 issues", TextRole.Muted)]);

        var line = string.Concat(grid.Row(0, static (_, _) => _noText, chips).Select(span => span.Text));

        Assert.Equal("╭─ To Do 5 ─ 5 issues ─╮", line);
        Assert.Equal(24, line.Length);
    }

    [Fact]
    public void GivenAFocusedPane_WhenReadingBorderRoles_ThenItsWholeBoxIsFocusedIncludingSharedEdges()
    {
        var grid = new FrameGrid(
            22,
            4,
            [new PaneRect(0, 0, 4, 8), new PaneRect(0, 7, 4, 8), new PaneRect(0, 14, 4, 8)],
            1);

        Assert.Equal(TextRole.Border, grid.BorderRole(1, 0));
        Assert.Equal(TextRole.Border, grid.BorderRole(0, 3));
        Assert.Equal(TextRole.FrameFocus, grid.BorderRole(1, 7));
        Assert.Equal(TextRole.FrameFocus, grid.BorderRole(0, 10));
        Assert.Equal(TextRole.FrameFocus, grid.BorderRole(3, 14));
        Assert.Equal(TextRole.Border, grid.BorderRole(1, 21));
    }

    [Fact]
    public void GivenNoFocusedPane_WhenReadingBorderRoles_ThenEveryBorderIsQuiet()
    {
        var grid = new FrameGrid(10, 4, [new PaneRect(0, 0, 4, 10)]);

        Assert.All(
            grid.Row(0, static (_, _) => _noText),
            span => Assert.Equal(TextRole.Border, span.Role));
    }

    [Fact]
    public void GivenFocusedAndDimChips_WhenPlacing_ThenTheLeftChipIsFilledOnlyWhenFocusedAndTheRightChipIsDim()
    {
        var pane = new PaneRect(0, 0, 3, 40);
        StyledSpan[] left = [new("Backlog", TextRole.Heading)];
        StyledSpan[] right = [new("8 issues", TextRole.Strong)];

        var focused = FrameGrid.Chips(pane, left, right, true);
        var dim = FrameGrid.Chips(pane, left, right);

        Assert.All(focused[0].Spans, span => Assert.Equal(TextRole.ChipFocus, span.Role));
        Assert.All(dim[0].Spans, span => Assert.Equal(TextRole.Muted, span.Role));
        Assert.All(focused[1].Spans, span => Assert.NotEqual(TextRole.ChipFocus, span.Role));
        Assert.Equal(focused[0].Width, dim[0].Width);
    }

    [Fact]
    public void GivenAChipWiderThanThePane_WhenPlacing_ThenItIsCutWithAnEllipsisAndNeverTouchesTheCorners()
    {
        var pane = new PaneRect(0, 0, 3, 12);
        var grid = new FrameGrid(12, 3, [pane]);
        var chips = FrameGrid.Chips(pane, [new StyledSpan("A very long column name", TextRole.Heading)]);

        var line = string.Concat(grid.Row(0, static (_, _) => _noText, chips).Select(span => span.Text));

        Assert.Equal(12, line.Length);
        Assert.StartsWith("╭─ ", line);
        Assert.EndsWith("…", line.TrimEnd('╮', '─', ' '));
        Assert.EndsWith("─╮", line);
    }

    [Fact]
    public void GivenAPaneNarrowerThanEight_WhenPlacingChips_ThenNoneIsDrawn()
    {
        Assert.Empty(FrameGrid.Chips(new PaneRect(0, 0, 3, 7), [new StyledSpan("x", TextRole.Heading)]));
    }

    [Theory]
    [InlineData(19, 10, false)]
    [InlineData(20, 4, false)]
    [InlineData(20, 5, true)]
    public void GivenASize_WhenAskingIfItGetsAFrame_ThenBelowTwentyByFiveItDoesNot(int width, int height, bool expected)
    {
        Assert.Equal(expected, FrameGrid.CanFrame(width, height));
    }

    [Fact]
    public void GivenPanesOutsideTheGridOrTooSmall_WhenBuilding_ThenNothingThrowsAndNothingIsDrawnForThem()
    {
        var grid = new FrameGrid(5, 3, [new PaneRect(0, 0, 1, 1), new PaneRect(-4, -4, 20, 20), new PaneRect(9, 9, 3, 3)]);

        Assert.Equal(5, Row(grid, 1).Length);
        Assert.False(grid.IsBorder(7, 7));
        Assert.Equal(string.Empty, grid.Glyph(7, 7));
    }

    private static string Row(FrameGrid grid, int row)
    {
        var spans = grid.Row(row, static (_, length) => [new StyledSpan(new string(' ', length), TextRole.Body)]);

        return string.Concat(spans.Select(span => span.Text));
    }
}
