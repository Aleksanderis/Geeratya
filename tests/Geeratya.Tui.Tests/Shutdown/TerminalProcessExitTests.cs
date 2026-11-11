using Geeratya.App.Tests.Fakes;
using Geeratya.Platform.Directories;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Shutdown;

namespace Geeratya.Tui.Tests.Shutdown;

[Trait("Os", "Independent")]
public sealed class TerminalProcessExitTests : IDisposable
{
    private readonly ManualTimeProvider _clock = new();
    private readonly BlockableEnder _ender = new();
    private readonly StringWriter _output = new();
    private readonly UiInvokeGate _gate = new();
    private readonly List<TerminalProcessExit> _exits = [];
    private readonly List<string> _events = [];
    private readonly RecordingRelease _release;

    public TerminalProcessExitTests()
    {
        _release = new RecordingRelease(_events, _output);
    }

    [Fact]
    public void GivenEnding_WhenRestoring_ThenTheResetSequencesAreWrittenBeforeTheApplicationIsReleased()
    {
        var exit = CreateExit(OperatingSystemKind.Linux, true);

        exit.EndProcess();

        Assert.Equal([$"release after {TerminalProcessExit.ResetSequences}"], _events);
    }

    [Fact]
    public async Task GivenExitThatBlocks_WhenTheDeadlinePasses_ThenTheProcessIsKilled()
    {
        _ender.BlockExit = true;

        var exit = CreateExit(OperatingSystemKind.Linux, true);
        var ending = Task.Run(exit.EndProcess);

        await _ender.ExitCalled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        _clock.Advance(TerminalProcessExit.KillDeadline - TimeSpan.FromMilliseconds(100));

        Assert.Equal(0, _ender.Kills);

        _clock.Advance(TimeSpan.FromMilliseconds(200));

        Assert.Equal(1, _ender.Kills);

        _ender.Unblock();
        await ending.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void GivenExitThatReturns_WhenEnding_ThenItExitsWithCodeOneAndTheGateIsClosed()
    {
        var exit = CreateExit(OperatingSystemKind.Linux, true);

        exit.EndProcess();

        Assert.Equal([TerminalProcessExit.ExitCode], _ender.ExitCodes);
        Assert.True(_gate.IsClosed);
        Assert.Equal(TerminalProcessExit.ResetSequences, _output.ToString());
    }

    [Fact]
    public void GivenEndedTwice_WhenEnding_ThenOnlyOneKillTimerIsArmed()
    {
        var exit = CreateExit(OperatingSystemKind.Linux, true);

        exit.EndProcess();
        exit.EndProcess();

        Assert.Equal(1, _clock.PendingTimers);
    }

    [Fact]
    public void GivenWindowsConsoleWithoutVirtualTerminal_WhenEnding_ThenNoSequencesArePrinted()
    {
        var exit = CreateExit(OperatingSystemKind.Windows, false);

        exit.EndProcess();

        Assert.Equal(string.Empty, _output.ToString());
        Assert.Single(_ender.ExitCodes);
    }

    [Fact]
    public void GivenWindowsConsoleWithVirtualTerminal_WhenEnding_ThenSequencesArePrinted()
    {
        var exit = CreateExit(OperatingSystemKind.Windows, true);

        exit.EndProcess();

        Assert.Equal(TerminalProcessExit.ResetSequences, _output.ToString());
    }

    public void Dispose()
    {
        foreach (var exit in _exits)
        {
            exit.Dispose();
        }

        _ender.Unblock();
        _ender.Dispose();
        _output.Dispose();
    }

    private TerminalProcessExit CreateExit(OperatingSystemKind system, bool sequencesEnabled)
    {
        var exit = new TerminalProcessExit(
            _release,
            _output,
            _gate,
            new TerminalReset(system, new FixedProbe(sequencesEnabled)),
            _ender,
            _clock);

        _exits.Add(exit);

        return exit;
    }

    private sealed class RecordingRelease : IApplicationRelease
    {
        private readonly List<string> _events;
        private readonly StringWriter _output;

        public RecordingRelease(List<string> events, StringWriter output)
        {
            _events = events;
            _output = output;
        }

        public void Release()
        {
            _events.Add($"release after {_output}");
        }
    }

    private sealed class FixedProbe : IConsoleModeProbe
    {
        private readonly bool _enabled;

        public FixedProbe(bool enabled)
        {
            _enabled = enabled;
        }

        public bool OutputSequencesEnabled()
        {
            return _enabled;
        }
    }

    private sealed class BlockableEnder : IProcessEnder, IDisposable
    {
        private readonly ManualResetEventSlim _release = new();
        private int _kills;

        public bool BlockExit { get; set; }

        public TaskCompletionSource ExitCalled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<int> ExitCodes { get; } = [];

        public int Kills => Volatile.Read(ref _kills);

        public void ExitWithCode(int exitCode)
        {
            ExitCodes.Add(exitCode);
            ExitCalled.TrySetResult();

            if (BlockExit)
            {
                _release.Wait(TimeSpan.FromSeconds(10));
            }
        }

        public void Kill()
        {
            Interlocked.Increment(ref _kills);
        }

        public void Unblock()
        {
            _release.Set();
        }

        public void Dispose()
        {
            _release.Dispose();
        }
    }
}
