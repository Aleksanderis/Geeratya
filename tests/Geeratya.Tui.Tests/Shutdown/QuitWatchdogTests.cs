using Geeratya.App.Tests.Fakes;
using Geeratya.Tui.Shutdown;

namespace Geeratya.Tui.Tests.Shutdown;

[Trait("Os", "Independent")]
public sealed class QuitWatchdogTests : IDisposable
{
    private readonly ManualTimeProvider _clock = new();
    private readonly CountingExit _exit = new();
    private readonly QuitWatchdog _watchdog;

    public QuitWatchdogTests()
    {
        _watchdog = new QuitWatchdog(_clock, _exit);
    }

    [Fact]
    public void GivenArmedQuit_WhenTheLoopNeverEnds_ThenTheProcessIsEndedAtTheDeadline()
    {
        _watchdog.Arm(TimeSpan.FromSeconds(3));

        _clock.Advance(TimeSpan.FromSeconds(2.9));

        Assert.Equal(0, _exit.Count);

        _clock.Advance(TimeSpan.FromSeconds(0.2));

        Assert.Equal(1, _exit.Count);
    }

    [Fact]
    public void GivenArmedQuit_WhenTheApplicationEndsByItself_ThenTheProcessIsLeftAlone()
    {
        _watchdog.Arm(TimeSpan.FromSeconds(3));
        _watchdog.Disarm();

        _clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(0, _exit.Count);
    }

    [Fact]
    public void GivenAnEarlierDeadline_WhenArmingLater_ThenTheEarlierOneStays()
    {
        _watchdog.Arm(TimeSpan.FromSeconds(3));
        _watchdog.Arm(TimeSpan.FromSeconds(30));

        _clock.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal(1, _exit.Count);
    }

    [Fact]
    public void GivenALongWaitArmed_WhenASignalArrives_ThenTheShorterDeadlineApplies()
    {
        _watchdog.Arm(TimeSpan.FromSeconds(8));

        Assert.True(_watchdog.Terminate(TimeSpan.FromSeconds(3)));

        _clock.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal(1, _exit.Count);
    }

    [Fact]
    public void GivenOneSignal_WhenASecondArrives_ThenTheProcessEndsAtOnce()
    {
        Assert.True(_watchdog.Terminate(TimeSpan.FromSeconds(3)));
        Assert.Equal(0, _exit.Count);

        Assert.False(_watchdog.Terminate(TimeSpan.FromSeconds(3)));
        Assert.Equal(1, _exit.Count);
    }

    public void Dispose()
    {
        _watchdog.Dispose();
    }

    private sealed class CountingExit : IProcessExit
    {
        public int Count { get; private set; }

        public void EndProcess()
        {
            Count++;
        }
    }
}
