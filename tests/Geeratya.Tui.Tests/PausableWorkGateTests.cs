namespace Geeratya.Tui.Tests;

[Trait("Os", "Independent")]
public class PausableWorkGateTests
{
    [Fact]
    public void GivenARunningQueue_WhenOfferingWork_ThenItIsNotHeld()
    {
        var queue = new PausableWorkGate();

        Assert.False(queue.TryHold(static () => { }));
        Assert.False(queue.IsPaused);
        Assert.Empty(queue.Release());
    }

    [Fact]
    public void GivenAPausedQueue_WhenReleasing_ThenHeldWorkComesBackOldestFirstAndTheQueueRuns()
    {
        var queue = new PausableWorkGate();
        var order = new List<int>();

        queue.Pause();

        Assert.True(queue.IsPaused);
        Assert.True(queue.TryHold(() => order.Add(1)));
        Assert.True(queue.TryHold(() => order.Add(2)));

        foreach (var work in queue.Release())
        {
            work();
        }

        Assert.Equal([1, 2], order);
        Assert.False(queue.IsPaused);
        Assert.False(queue.TryHold(static () => { }));
        Assert.Empty(queue.Release());
    }
}
