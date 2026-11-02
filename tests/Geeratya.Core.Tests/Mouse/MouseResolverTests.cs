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
        var command = MouseResolver.Resolve(_normal, 5, 10, MouseGesture.WheelDown, default);

        Assert.Equal(new ActionRequested(ActionIds.NavigateDown, 3), command);
    }

    [Fact]
    public void GivenClickOnStatusLine_WhenResolving_ThenOpensPalette()
    {
        var command = MouseResolver.Resolve(_normal, 5, 23, MouseGesture.Click, default);

        Assert.Equal(ActionIds.PaletteOpen, command!.Action);
    }

    [Fact]
    public void GivenOverlayAndClickOnTabRow_WhenResolving_ThenCancels()
    {
        var context = _normal with { OverlayOpen = true };

        Assert.Equal(ActionIds.Cancel, MouseResolver.Resolve(context, 5, 0, MouseGesture.Click, default)!.Action);
    }

    [Fact]
    public void GivenOverlayAndWheel_WhenResolving_ThenNothing()
    {
        var context = _normal with { OverlayOpen = true };

        Assert.Null(MouseResolver.Resolve(context, 5, 10, MouseGesture.WheelDown, default));
    }

    [Fact]
    public void GivenRowOfALaterPart_WhenResolving_ThenNothingYet()
    {
        Assert.Null(MouseResolver.Resolve(_normal, 5, 10, MouseGesture.DoubleClick, default));
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
}
