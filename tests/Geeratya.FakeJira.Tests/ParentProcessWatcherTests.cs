using System.Diagnostics;

namespace Geeratya.FakeJira.Tests;

public sealed class ParentProcessWatcherTests
{
    [Fact]
    public void GivenOwnProcess_WhenCheckedForGone_ThenItIsRunning()
    {
        Assert.False(ParentProcessWatcher.IsGone(Environment.ProcessId));
    }

    [Fact]
    public async Task GivenExitedProcess_WhenWaitingForIt_ThenWaitReturnsWithoutStopSignal()
    {
        var parent = StartShortLivedProcess();
        using var stop = new ManualResetEventSlim();

        var waiter = Task.Run(() => ParentProcessWatcher.WaitForStopOrParentGone(parent, stop, TimeSpan.FromMilliseconds(50)));

        await waiter.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.False(stop.IsSet);
    }

    [Fact]
    public async Task GivenLiveParent_WhenStopIsSet_ThenWaitReturnsOnlyThen()
    {
        using var stop = new ManualResetEventSlim();
        var waiter = Task.Run(() => ParentProcessWatcher.WaitForStopOrParentGone(Environment.ProcessId, stop, TimeSpan.FromMilliseconds(50)));

        await Task.Delay(300);
        Assert.False(waiter.IsCompleted, "the wait ended while the parent was alive and no stop was set");
        stop.Set();

        await waiter.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void GivenParentPidFlag_WhenParsed_ThenItIsInTheOptions()
    {
        var options = FakeJiraOptionsParser.TryParse([FakeJiraOptionsParser.ParentPidFlag, "1234"], out var error);

        Assert.NotNull(options);
        Assert.Equal(1234, options.ParentProcessId);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void GivenNoParentPidFlag_WhenParsed_ThenWatchingIsOff()
    {
        var options = FakeJiraOptionsParser.TryParse([], out _);

        Assert.Equal(0, options?.ParentProcessId);
    }

    private static int StartShortLivedProcess()
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!, "--version")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(start)!;
        var id = process.Id;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return id;
    }
}
