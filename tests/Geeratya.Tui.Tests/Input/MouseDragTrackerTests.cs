using Geeratya.Tui.Input;
using Terminal.Gui.Input;

namespace Geeratya.Tui.Tests.Input;

[Trait("Os", "Independent")]
public class MouseDragTrackerTests
{
    [Fact]
    public void GivenPressThenReleaseOnAnotherCell_WhenTracking_ThenTheDragIsReported()
    {
        var tracker = new MouseDragTracker();

        Assert.Null(tracker.Track(MouseFlags.LeftButtonPressed, 5, 3));

        var drag = tracker.Track(MouseFlags.LeftButtonReleased, 40, 4);

        Assert.Equal(new MouseDrag(5, 3, 40, 4), drag);
    }

    [Fact]
    public void GivenPressMotionThenRelease_WhenTheMotionCarriesThePressedFlag_ThenTheDragStartsAtThePress()
    {
        var tracker = new MouseDragTracker();
        var motion = MouseFlags.LeftButtonPressed | MouseFlags.PositionReport;

        tracker.Track(MouseFlags.LeftButtonPressed, 5, 3);

        Assert.Null(tracker.Track(motion, 20, 3));
        Assert.Null(tracker.Track(motion, 40, 4));
        Assert.Equal(new MouseDrag(5, 3, 40, 4), tracker.Track(MouseFlags.LeftButtonReleased, 40, 4));
    }

    [Fact]
    public void GivenPressThenReleaseOnTheSameCell_WhenTracking_ThenNoDragAndTheClickIsNotSwallowed()
    {
        var tracker = new MouseDragTracker();

        tracker.Track(MouseFlags.LeftButtonPressed, 5, 3);

        Assert.Null(tracker.Track(MouseFlags.LeftButtonReleased, 5, 3));
        Assert.False(tracker.SwallowsClick(MouseFlags.LeftButtonClicked));
    }

    [Fact]
    public void GivenADrag_WhenAClickFollows_ThenItIsSwallowedOnceOnly()
    {
        var tracker = new MouseDragTracker();

        tracker.Track(MouseFlags.LeftButtonPressed, 5, 3);
        tracker.Track(MouseFlags.LeftButtonReleased, 40, 4);

        Assert.True(tracker.SwallowsClick(MouseFlags.LeftButtonClicked));
        Assert.False(tracker.SwallowsClick(MouseFlags.LeftButtonClicked));
    }

    [Fact]
    public void GivenADragWithoutAFollowingClick_WhenPressingAgain_ThenTheNextClickActs()
    {
        var tracker = new MouseDragTracker();

        tracker.Track(MouseFlags.LeftButtonPressed, 5, 3);
        tracker.Track(MouseFlags.LeftButtonReleased, 40, 4);
        tracker.Track(MouseFlags.LeftButtonPressed, 7, 7);

        Assert.False(tracker.SwallowsClick(MouseFlags.LeftButtonClicked));
    }

    [Fact]
    public void GivenAReleaseWithoutPress_WhenTracking_ThenNothingIsReported()
    {
        var tracker = new MouseDragTracker();

        Assert.Null(tracker.Track(MouseFlags.LeftButtonReleased, 5, 3));
        Assert.Null(tracker.Track(MouseFlags.WheeledDown, 5, 3));
    }
}
