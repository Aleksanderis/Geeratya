using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;

namespace Geeratya.Core.Tests.Mouse;

[Trait("Os", "Independent")]
public class MouseResolverTests
{
    private static readonly MouseContext _normal = new(80, 24, 2, false, false);

    [Fact]
    public void GivenWheelDownInBody_WhenResolving_ThenNavigateDownThreeTimes()
    {
        var commands = MouseResolver.Resolve(_normal, 5, 10, MouseGesture.WheelDown, default);

        Assert.Equal(new ActionRequested(ActionIds.NavigateDown, 3), Assert.Single(commands));
    }

    [Fact]
    public void GivenARowRegion_WhenClickingAndRightClicking_ThenTheRowCommandCarriesTheTableAction()
    {
        OverlayRegion[] regions = [new(MouseTarget.ProfileRow, 4, 1, 30, 1, 2)];
        var context = new MouseContext(80, 24, 2, true, false) { Regions = regions };

        var click = MouseResolver.Resolve(context, 5, 4, MouseGesture.Click, default);
        var right = MouseResolver.Resolve(context, 5, 4, MouseGesture.RightClick, default);
        var wheel = MouseResolver.Resolve(context, 5, 4, MouseGesture.WheelDown, default);

        Assert.Equal(new OverlayRowClicked(MouseTarget.ProfileRow, 2, ActionIds.FormNext), Assert.Single(click));
        Assert.Equal(new OverlayRowClicked(MouseTarget.ProfileRow, 2, ActionIds.ProfileRemove), Assert.Single(right));
        Assert.Empty(wheel);
        Assert.Empty(MouseResolver.Resolve(context, 6, 8, MouseGesture.Click, default));
    }

    [Fact]
    public void GivenClickOnStatusLine_WhenResolving_ThenOpensPalette()
    {
        var commands = MouseResolver.Resolve(_normal, 5, 23, MouseGesture.Click, default);

        Assert.Equal(ActionIds.PaletteOpen, Assert.IsType<ActionRequested>(Assert.Single(commands)).Action);
    }

    [Fact]
    public void GivenOverlayAndClickOnTabRow_WhenResolving_ThenCancels()
    {
        var context = _normal with { OverlayOpen = true };

        var commands = MouseResolver.Resolve(context, 5, 0, MouseGesture.Click, default);

        Assert.Equal(ActionIds.Cancel, Assert.IsType<ActionRequested>(Assert.Single(commands)).Action);
    }

    [Fact]
    public void GivenOverlayAndWheel_WhenResolving_ThenNothing()
    {
        var context = _normal with { OverlayOpen = true };

        Assert.Empty(MouseResolver.Resolve(context, 5, 10, MouseGesture.WheelDown, default));
    }

    [Fact]
    public void GivenCtrlClickOnARow_WhenResolving_ThenThePointerMarksThatRowInPlace()
    {
        var commands = MouseResolver.Resolve(_normal, 5, 10, MouseGesture.CtrlClick, default);

        var placed = Assert.IsType<PointerPlaced>(Assert.Single(commands));

        Assert.Equal(new ActionRequested(ActionIds.SelectionMark, 0), placed.Then);
    }

    [Fact]
    public void GivenADragOverRowsInTheListLayout_WhenResolving_ThenRowsAreDragged()
    {
        var at = new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

        var commands = MouseResolver.ResolveDrag(_normal, 5, 10, 5, 13, at);

        var dragged = Assert.IsType<RowsDragged>(Assert.Single(commands));

        Assert.Equal(new RowsDragged(5, dragged.FromRow, dragged.ToRow, _normal.Width, at), dragged);
        Assert.True(dragged.ToRow > dragged.FromRow);
    }

    [Fact]
    public void GivenTheHeldButtonOverRows_WhenMoving_ThenTheLiveRangeCommandCarriesPressAndPointerRows()
    {
        var first = MouseResolver.ResolveDragMove(_normal, 5, 10, 5, 12, true);
        var later = MouseResolver.ResolveDragMove(_normal, 5, 10, 5, 14, false);

        Assert.Equal(new RowsDragging(5, 8, 10, _normal.Width, true), Assert.Single(first));
        Assert.Equal(new RowsDragging(5, 8, 12, _normal.Width, false), Assert.Single(later));
    }

    [Fact]
    public void GivenTheHeldButtonOutsideTheBody_WhenMoving_ThenOnlyTheFirstMoveStartsTheRange()
    {
        var first = MouseResolver.ResolveDragMove(_normal, 5, 10, 5, 0, true);
        var later = MouseResolver.ResolveDragMove(_normal, 5, 10, 5, 23, false);

        Assert.Equal(new RowsDragging(5, 8, 8, _normal.Width, true), Assert.Single(first));
        Assert.Empty(later);
    }

    [Fact]
    public void GivenColumnsOverlayOrPressOutsideTheBody_WhenMoving_ThenNothingIsResolved()
    {
        Assert.Empty(MouseResolver.ResolveDragMove(_normal with { ColumnsLayout = true }, 5, 10, 5, 12, true));
        Assert.Empty(MouseResolver.ResolveDragMove(_normal with { OverlayOpen = true }, 5, 10, 5, 12, true));
        Assert.Empty(MouseResolver.ResolveDragMove(_normal, 5, 0, 5, 12, true));
        Assert.Empty(MouseResolver.ResolveDragMove(_normal, 5, 23, 5, 12, true));
    }

    [Fact]
    public void GivenTheReleaseOfALiveRange_WhenResolving_ThenTheDragIsMarkedLive()
    {
        var commands = MouseResolver.ResolveDrag(_normal, 5, 10, 5, 13, default, true);
        var dragged = Assert.IsType<RowsDragged>(Assert.Single(commands));

        Assert.True(dragged.Live);
    }

    [Fact]
    public void GivenARangeStartedInTheBody_WhenThePointerMovesInsideThenBelowIt_ThenTheSpeedBecomesNonZero()
    {
        Assert.Equal(0, MouseResolver.DragScrollStep(_normal, 5, 10, 12));
        Assert.Equal(1, MouseResolver.DragScrollStep(_normal, 5, 10, 23));
        Assert.Equal(-2, MouseResolver.DragScrollStep(_normal, 5, 10, 0));
    }

    [Fact]
    public void GivenAPressThatStartsNoLiveRange_WhenThePointerIsOutside_ThenThereIsNoScroll()
    {
        Assert.Equal(0, MouseResolver.DragScrollStep(_normal with { ColumnsLayout = true }, 5, 10, 23));
        Assert.Equal(0, MouseResolver.DragScrollStep(_normal with { OverlayOpen = true }, 5, 10, 23));
        Assert.Equal(0, MouseResolver.DragScrollStep(_normal, 5, 0, 23));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-3, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(5, 3)]
    [InlineData(9, 5)]
    [InlineData(11, 6)]
    [InlineData(40, 6)]
    public void GivenADistanceOutsideTheBody_WhenLookingUpTheSpeed_ThenItFollowsTheTable(int distance, int rows)
    {
        Assert.Equal(rows, DragAutoScroll.RowsFor(distance));
    }

    [Fact]
    public void GivenThePointerAboveInsideAndBelowTheBody_WhenAskingForTheStep_ThenTheSignFollowsTheSide()
    {
        // Header rows 2, list rows start at screen row 4 (two list header rows), the last body row is 22.
        Assert.Equal(-1, DragAutoScroll.StepFor(_normal, 3));
        Assert.Equal(-2, DragAutoScroll.StepFor(_normal, 0));
        Assert.Equal(0, DragAutoScroll.StepFor(_normal, 4));
        Assert.Equal(0, DragAutoScroll.StepFor(_normal, 22));
        Assert.Equal(1, DragAutoScroll.StepFor(_normal, 23));
    }

    [Fact]
    public void GivenADragInTheColumnsLayout_WhenResolving_ThenACardIsDraggedNotRows()
    {
        var context = _normal with { ColumnsLayout = true };

        var commands = MouseResolver.ResolveDrag(context, 5, 10, 40, 10);

        Assert.IsType<CardDragged>(Assert.Single(commands));
    }

    [Fact]
    public void GivenActionRequested_WhenReducing_ThenRunsTheKeysHandler()
    {
        var reducer = new AppReducer(DefaultKeymap.Load());

        var opened = reducer.Reduce(AppState.Initial, new ActionRequested(ActionIds.PaletteOpen));
        var cancelled = reducer.Reduce(opened.State, new ActionRequested(ActionIds.Cancel));

        Assert.Equal(InputMode.Command, opened.State.Mode);
        Assert.Equal(InputMode.Normal, cancelled.State.Mode);
    }

    [Fact]
    public void GivenCountedAction_WhenReducing_ThenDoesNotThrowOnEmptyState()
    {
        var reducer = new AppReducer(DefaultKeymap.Load());

        var result = reducer.Reduce(AppState.Initial, new ActionRequested(ActionIds.NavigateDown, 3));

        Assert.Equal(InputMode.Normal, result.State.Mode);
    }

    [Theory]
    [InlineData(InputMode.Help)]
    [InlineData(InputMode.Command)]
    [InlineData(InputMode.Confirm)]
    public void GivenModeWithOverlay_WhenBuildingContext_ThenOverlayIsOpen(InputMode mode)
    {
        var state = AppState.Initial with { Mode = mode };

        Assert.True(MouseContext.From(state, 80, 24, 2, default).OverlayOpen);
    }

    [Fact]
    public void GivenNormalModeWithoutTabs_WhenBuildingContext_ThenNoOverlayAndNoDetail()
    {
        var context = MouseContext.From(AppState.Initial, 80, 24, 2, default);

        Assert.Equal(new MouseContext(80, 24, 2, false, false), context with { TabAreas = [] });
        Assert.Empty(context.TabAreas);
    }

    [Theory]
    [InlineData(InputMode.Help)]
    [InlineData(InputMode.Command)]
    [InlineData(InputMode.Confirm)]
    public void GivenOpenMode_WhenCancelRequested_ThenSameStateAsEsc(InputMode mode)
    {
        var reducer = new AppReducer(DefaultKeymap.Load());
        var state = AppState.Initial with { Mode = mode };

        var byMouse = reducer.Reduce(state, new ActionRequested(ActionIds.Cancel)).State;
        var byKey = reducer.Reduce(state, new KeyPressed("Esc")).State;

        Assert.Equal(byKey, byMouse with { StatusHistory = byKey.StatusHistory });
    }
}
