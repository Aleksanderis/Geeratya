using Geeratya.App.Tests.Fakes;
using Geeratya.Tui.Shutdown;

namespace Geeratya.Tui.Tests.Shutdown;

[Trait("Os", "Independent")]
public sealed class UiStallMonitorTests : IDisposable
{
    private readonly ManualTimeProvider _clock = new();
    private readonly RecordingReport _report = new();
    private readonly UiStallMonitor _monitor;

    public UiStallMonitorTests()
    {
        _monitor = new UiStallMonitor(_clock, _report, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GivenALoopThatKeepsBeating_WhenChecking_ThenNothingIsReported()
    {
        for (var second = 0; second < 20; second++)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            _monitor.Beat();
            _monitor.Check();
        }

        Assert.Empty(_report.Lines);
    }

    [Fact]
    public void GivenALoopThatStops_WhenTheThresholdPasses_ThenOneStallIsReportedWithTheLastWork()
    {
        _monitor.Note("Tick");
        _clock.Advance(TimeSpan.FromSeconds(4));
        _monitor.Check();

        Assert.Empty(_report.Lines);

        _clock.Advance(TimeSpan.FromSeconds(2));
        _monitor.Check();
        _clock.Advance(TimeSpan.FromSeconds(2));
        _monitor.Check();

        Assert.Equal(["stalled 6 Tick"], _report.Lines);
    }

    [Fact]
    public void GivenAReportedStall_WhenTheLoopBeatsAgain_ThenRecoveryIsReportedOnce()
    {
        _clock.Advance(TimeSpan.FromSeconds(7));
        _monitor.Check();
        _monitor.Beat();
        _monitor.Check();
        _monitor.Check();

        Assert.Equal(["stalled 7 none", "recovered 7"], _report.Lines);
    }

    [Fact]
    public void GivenSuspendedMonitor_WhenNoLoopRuns_ThenNothingIsReportedUntilResumed()
    {
        _monitor.Suspend();
        _clock.Advance(TimeSpan.FromMinutes(5));
        _monitor.Check();

        Assert.Empty(_report.Lines);

        _monitor.Resume();
        _clock.Advance(TimeSpan.FromSeconds(1));
        _monitor.Check();

        Assert.Empty(_report.Lines);
    }

    public void Dispose()
    {
        _monitor.Dispose();
    }

    private sealed class RecordingReport : IStallReport
    {
        public List<string> Lines { get; } = [];

        public void Stalled(TimeSpan stalled, string lastWork)
        {
            Lines.Add($"stalled {stalled.TotalSeconds:0} {lastWork}");
        }

        public void Recovered(TimeSpan stalled)
        {
            Lines.Add($"recovered {stalled.TotalSeconds:0}");
        }
    }
}
