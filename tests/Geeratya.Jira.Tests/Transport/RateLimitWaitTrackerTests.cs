using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Tests.Transport;

public sealed class RateLimitWaitTrackerTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 1, 12, 0, 0, default);

    [Fact]
    public async Task GivenTwoOverlappingWaits_WhenOneEnds_ThenUntilIsTheOtherEndAndNullAfterBoth()
    {
        var tracker = new RateLimitWaitTracker();
        var first = new TaskCompletionSource();
        var second = new TaskCompletionSource();

        var one = tracker.RunAsync(_start.AddSeconds(2), () => first.Task);
        var two = tracker.RunAsync(_start.AddSeconds(5), () => second.Task);
        Assert.Equal(_start.AddSeconds(5), tracker.Until);

        second.SetResult();
        await two;
        Assert.Equal(_start.AddSeconds(2), tracker.Until);

        first.SetResult();
        await one;
        Assert.Null(tracker.Until);
    }

    [Fact]
    public async Task GivenWaitIsCancelled_WhenItThrows_ThenUntilIsCleared()
    {
        var tracker = new RateLimitWaitTracker();

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => tracker.RunAsync(_start.AddSeconds(2), () => Task.FromCanceled(new CancellationToken(true))));

        Assert.Null(tracker.Until);
    }

    [Fact]
    public async Task GivenThrowingHandler_WhenWaiting_ThenWaitCompletesAndUntilIsCleared()
    {
        var tracker = new RateLimitWaitTracker();
        tracker.Changed += () => throw new InvalidOperationException("ui bug");

        await tracker.RunAsync(_start.AddSeconds(2), () => Task.CompletedTask);

        Assert.Null(tracker.Until);
    }
}
