using Geeratya.Tui.Input;
using Geeratya.Tui.Tests.Fakes;

namespace Geeratya.Tui.Tests.Input;

[Trait("Os", "Independent")]
public class DragAutoScrollerTests
{
    private readonly ManualTimeProvider _time = new();

    [Fact]
    public void GivenASpeed_WhenTheTimerFires_ThenEachFireStepsWithTheCurrentRows()
    {
        using var scroller = new DragAutoScroller(_time);
        var steps = new List<int>();

        scroller.Stepped += (_, rows) => steps.Add(rows);
        scroller.Start(2);
        _time.Fire();
        scroller.Start(5);
        _time.Fire();

        Assert.Equal([2, 5], steps);
        Assert.Equal(1, _time.Created);
    }

    [Fact]
    public void GivenARunningScroll_WhenStopped_ThenTheTimerIsGoneAndNothingSteps()
    {
        using var scroller = new DragAutoScroller(_time);
        var steps = 0;

        scroller.Stepped += (_, _) => steps++;
        scroller.Start(-3);
        scroller.Stop();
        _time.Fire();

        Assert.Equal(0, steps);
        Assert.Equal(0, scroller.Rows);
        Assert.Equal(1, _time.Disposed);
    }

    [Fact]
    public void GivenARunningScroll_WhenTheSpeedBecomesZero_ThenItStops()
    {
        using var scroller = new DragAutoScroller(_time);

        scroller.Start(1);
        scroller.Start(0);

        Assert.Equal(1, _time.Disposed);
    }
}
