using Geeratya.Core.Panes;

namespace Geeratya.Core.Tests.Panes;

/// <summary>Pure pane model: focus over a grid, zoom state and scroll clamping.</summary>
[Trait("Os", "Independent")]
public class PaneModelTests
{
    // The wide detail: four stacked panes on the left, the Details sidebar spanning all rows on the right.
    private static readonly PaneGrid _wide = new(
    [
        new PaneSlot("title", 0, 0),
        new PaneSlot("description", 0, 1),
        new PaneSlot("subtasks", 0, 2),
        new PaneSlot("comments", 0, 3),
        new PaneSlot("details", 1, 0, 1, 4),
    ]);

    private static readonly PaneGrid _narrow = new(
    [
        new PaneSlot("title", 0, 0),
        new PaneSlot("description", 0, 1),
        new PaneSlot("details", 0, 2),
    ]);

    [Theory]
    [InlineData("title", PaneDirection.Down, "description")]
    [InlineData("description", PaneDirection.Down, "subtasks")]
    [InlineData("comments", PaneDirection.Up, "subtasks")]
    [InlineData("description", PaneDirection.Right, "details")]
    [InlineData("details", PaneDirection.Left, "title")]
    public void GivenTheWideGrid_WhenFocusMoves_ThenItReachesTheNeighbour(
        string from,
        PaneDirection direction,
        string expected)
    {
        Assert.Equal(expected, PaneNavigator.Move(_wide, from, direction));
    }

    [Theory]
    [InlineData("title", PaneDirection.Up)]
    [InlineData("title", PaneDirection.Left)]
    [InlineData("comments", PaneDirection.Down)]
    [InlineData("details", PaneDirection.Right)]
    [InlineData("details", PaneDirection.Up)]
    [InlineData("details", PaneDirection.Down)]
    public void GivenTheWideGrid_WhenFocusMovesOverAnEdge_ThenItStaysPut(string from, PaneDirection direction)
    {
        Assert.Equal(from, PaneNavigator.Move(_wide, from, direction));
    }

    [Fact]
    public void GivenTheNarrowGrid_WhenFocusMovesDown_ThenDetailsComesAfterDescription()
    {
        Assert.Equal("details", PaneNavigator.Move(_narrow, "description", PaneDirection.Down));
        Assert.Equal("description", PaneNavigator.Move(_narrow, "details", PaneDirection.Up));
        Assert.Equal("details", PaneNavigator.Move(_narrow, "details", PaneDirection.Right));
    }

    [Fact]
    public void GivenAnUnknownPaneOrAnEmptyGrid_WhenFocusMoves_ThenItIsReturnedUnchanged()
    {
        Assert.Equal("ghost", PaneNavigator.Move(_wide, "ghost", PaneDirection.Down));
        Assert.Equal("x", PaneNavigator.Move(PaneGrid.Empty, "x", PaneDirection.Left));
    }

    [Fact]
    public void GivenTwoPanesBesideTheMover_WhenMovingRight_ThenTheOneSharingMostRowsWins()
    {
        var grid = new PaneGrid(
        [
            new PaneSlot("tall", 0, 0, 1, 3),
            new PaneSlot("top", 1, 0),
            new PaneSlot("bottom", 1, 1, 1, 2),
        ]);

        Assert.Equal("bottom", PaneNavigator.Move(grid, "tall", PaneDirection.Right));
        Assert.Equal("tall", PaneNavigator.Move(grid, "bottom", PaneDirection.Left));
    }

    [Fact]
    public void GivenNoFocusYet_WhenCurrentIsAsked_ThenTheFirstPaneCounts()
    {
        var state = new PaneState(_wide);

        Assert.Equal("title", state.Current);
        Assert.Null(PaneState.None.Current);
    }

    [Fact]
    public void GivenAFocusedPane_WhenFocusMoves_ThenTheStateFollows()
    {
        var state = PaneReducer.Focus(new PaneState(_wide), PaneDirection.Down);
        state = PaneReducer.Focus(state, PaneDirection.Right);

        Assert.Equal("details", state.Focused);
        Assert.Null(state.Zoomed);
    }

    [Fact]
    public void GivenNoPanes_WhenFocusOrZoomIsRequested_ThenNothingChanges()
    {
        Assert.Equal(PaneState.None, PaneReducer.Focus(PaneState.None, PaneDirection.Down));
        Assert.Equal(PaneState.None, PaneReducer.ToggleZoom(PaneState.None));
    }

    [Fact]
    public void GivenAFocusedPane_WhenZoomTogglesTwice_ThenItZoomsAndLeaves()
    {
        var focused = PaneReducer.Focus(new PaneState(_wide), PaneDirection.Down);

        var zoomed = PaneReducer.ToggleZoom(focused);
        var left = PaneReducer.ToggleZoom(zoomed);

        Assert.Equal("description", zoomed.Zoomed);
        Assert.Null(left.Zoomed);
        Assert.Equal("description", left.Focused);
    }

    [Fact]
    public void GivenAZoom_WhenFocusMovesLeftOrRight_ThenTheSectionStripSteps()
    {
        var zoomed = PaneReducer.ToggleZoom(new PaneState(_narrow));

        var next = PaneReducer.Focus(zoomed, PaneDirection.Right);
        var last = PaneReducer.Focus(PaneReducer.Focus(next, PaneDirection.Right), PaneDirection.Right);
        var back = PaneReducer.Focus(next, PaneDirection.Left);

        Assert.Equal("description", next.Zoomed);
        Assert.Equal("description", next.Focused);
        Assert.Equal("details", last.Zoomed);
        Assert.Equal("title", back.Zoomed);
    }

    [Fact]
    public void GivenAZoom_WhenFocusMovesUpOrDownOrOffTheStrip_ThenTheZoomStays()
    {
        var zoomed = PaneReducer.ToggleZoom(new PaneState(_narrow));

        Assert.Equal("title", PaneReducer.Focus(zoomed, PaneDirection.Down).Zoomed);
        Assert.Equal("title", PaneReducer.Focus(zoomed, PaneDirection.Left).Zoomed);
    }

    [Fact]
    public void GivenAStripOrder_WhenZoomedAndSteppingRight_ThenTheStripOrderDecides()
    {
        var state = new PaneState(_narrow, Strip: ["description", "details", "ghost", "title"]);

        var zoomed = PaneReducer.ToggleZoom(state with { Focused = "description" });
        var next = PaneReducer.Focus(zoomed, PaneDirection.Right);
        var after = PaneReducer.Focus(next, PaneDirection.Right);

        Assert.Equal("details", next.Zoomed);
        Assert.Equal("title", after.Zoomed);
    }

    [Fact]
    public void GivenAZoom_WhenTheGridLosesThePane_ThenTheZoomAndFocusAreDropped()
    {
        var zoomed = PaneReducer.ToggleZoom(PaneReducer.Focus(new PaneState(_wide), PaneDirection.Right));

        var resized = PaneReducer.WithGrid(zoomed, new PaneGrid(_narrow.Slots.Take(2).ToList()));

        Assert.Null(resized.Zoomed);
        Assert.Equal("title", resized.Focused);
    }

    [Fact]
    public void GivenAScrollOfAVanishedPane_WhenTheGridChanges_ThenTheScrollIsDropped()
    {
        var state = PaneReducer.SetScroll(new PaneState(_wide), "comments", new PaneScroll(3, 20, 5));

        var narrow = PaneReducer.WithGrid(state, _narrow);

        Assert.Null(narrow.ScrollOf("comments"));
        Assert.NotNull(PaneReducer.WithGrid(state, _wide).ScrollOf("comments"));
    }

    [Theory]
    [InlineData(0, 14, 4, 5, 5)]
    [InlineData(0, 14, 4, 50, 10)]
    [InlineData(0, 14, 4, -3, 0)]
    [InlineData(8, 14, 4, 5, 10)]
    [InlineData(0, 3, 4, 2, 0)]
    [InlineData(0, 0, 4, 1, 0)]
    [InlineData(0, 14, 0, 20, 14)]
    public void GivenAScroll_WhenMovedByRows_ThenItIsClampedToTheContent(
        int offset,
        int total,
        int viewport,
        int rows,
        int expected)
    {
        Assert.Equal(expected, new PaneScroll(offset, total, viewport).By(rows).Offset);
    }

    [Fact]
    public void GivenAHugeStep_WhenScrolling_ThenItDoesNotOverflow()
    {
        Assert.Equal(10, new PaneScroll(5, 14, 4).By(int.MaxValue).Offset);
        Assert.Equal(0, new PaneScroll(5, 14, 4).By(int.MinValue).Offset);
    }

    [Fact]
    public void GivenContentThatShrinks_WhenResized_ThenTheOffsetIsReclamped()
    {
        var resized = new PaneScroll(10, 14, 4).Resized(6, 4);

        Assert.Equal(2, resized.Offset);
        Assert.Equal(0, resized.Resized(3, 4).Offset);
    }

    [Theory]
    [InlineData(0, "1-4 of 14")]
    [InlineData(10, "11-14 of 14")]
    [InlineData(99, "11-14 of 14")]
    public void GivenAScroll_WhenTheChipIsBuilt_ThenRowsAreOneBased(int offset, string expected)
    {
        Assert.Equal(expected, new PaneScroll(offset, 14, 4).Chip());
    }

    [Fact]
    public void GivenAShortContentOrNone_WhenTheChipIsBuilt_ThenItNamesTheRealCount()
    {
        Assert.Equal("1-3 of 3", new PaneScroll(0, 3, 5).Chip());
        Assert.Equal("0 of 0", new PaneScroll(0, 0, 5).Chip());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 3)]
    [InlineData(5, 3)]
    [InlineData(6, 3)]
    [InlineData(7, 4)]
    [InlineData(20, 10)]
    public void GivenARow_WhenRevealed_ThenTheLeastScrollShowsIt(int row, int expected)
    {
        Assert.Equal(expected, new PaneScroll(3, 14, 4).Reveal(row).Offset);
    }

    [Fact]
    public void GivenARowBelowTheFrame_WhenRevealed_ThenItIsTheLastVisibleRow()
    {
        var scroll = new PaneScroll(0, 14, 4);

        Assert.Equal(3, scroll.Reveal(6).Offset);
        Assert.Equal(10, scroll.Reveal(99).Offset);
        Assert.Equal(2, new PaneScroll(5, 14, 4).Reveal(2).Offset);
        Assert.Equal(5, new PaneScroll(5, 14, 4).Reveal(7).Offset);
    }

    [Fact]
    public void GivenContentThatFits_WhenTheBarIsBuilt_ThenThereIsNoBar()
    {
        Assert.Empty(ScrollBar.Cells(new PaneScroll(0, 4, 4), 6));
        Assert.Empty(ScrollBar.Cells(new PaneScroll(0, 40, 4), 2));
    }

    [Fact]
    public void GivenLongContent_WhenTheBarIsBuilt_ThenTheThumbSitsByTheOffset()
    {
        var top = ScrollBar.Cells(new PaneScroll(0, 14, 4), 6);
        var bottom = ScrollBar.Cells(new PaneScroll(10, 14, 4), 6);

        Assert.Equal([ScrollCell.Up, ScrollCell.Thumb, ScrollCell.Track, ScrollCell.Track, ScrollCell.Track, ScrollCell.Down], top);
        Assert.Equal([ScrollCell.Up, ScrollCell.Track, ScrollCell.Track, ScrollCell.Track, ScrollCell.Thumb, ScrollCell.Down], bottom);
    }

    [Fact]
    public void GivenVeryLongContent_WhenTheBarIsBuilt_ThenTheThumbIsAtLeastOneCell()
    {
        var cells = ScrollBar.Cells(new PaneScroll(0, 10_000, 4), 8);

        Assert.Equal(1, cells.Count(cell => cell == ScrollCell.Thumb));
        Assert.Equal(8, cells.Count);
    }
}
