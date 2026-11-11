using Geeratya.Platform.Terminal;

namespace Geeratya.Tui.Shutdown;

/// <summary>
/// Gives the terminal back and ends the process. The reset sequences are written first (while the console is still in
/// the mode the application set up) and the application is disposed after them, on a separate task that is waited on
/// for a second at most: either can block when the terminal or the UI thread is the thing that is stuck. A last-resort
/// timer kills the process if <c>Environment.Exit</c> itself blocks, for example on a ProcessExit handler waiting for
/// a lock the stuck thread holds (#940).
/// </summary>
public sealed class TerminalProcessExit : IProcessExit, IDisposable
{
    /// <summary>Leave the alternate screen, show the cursor, stop mouse and paste reporting, reset attributes.</summary>
    public const string ResetSequences =
        "\e[?1049l\e[?25h\e[?1000l\e[?1002l\e[?1003l\e[?1006l\e[?2004l\e[0m";

    public const int ExitCode = 1;

    /// <summary>How long an orderly exit may take before the process is killed.</summary>
    public static readonly TimeSpan KillDeadline = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan _restoreWait = TimeSpan.FromSeconds(1);

    private readonly ApplicationUiThread _uiThread;
    private readonly TextWriter _output;
    private readonly UiInvokeGate _gate;
    private readonly TerminalReset _reset;
    private readonly IProcessEnder _ender;
    private readonly TimeProvider _clock;
    private int _started;
    private ITimer? _killTimer;

    public TerminalProcessExit(
        ApplicationUiThread uiThread,
        TextWriter output,
        UiInvokeGate gate,
        TerminalReset reset,
        IProcessEnder ender,
        TimeProvider? clock = null)
    {
        _uiThread = uiThread;
        _output = output;
        _gate = gate;
        _reset = reset;
        _ender = ender;
        _clock = clock ?? TimeProvider.System;
    }

    public void EndProcess()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
        {
            _killTimer = _clock.CreateTimer(Kill, null, KillDeadline, Timeout.InfiniteTimeSpan);
        }

        try
        {
            Task.Run(Restore).Wait(_restoreWait);
        }
        catch (Exception exception) when (exception is AggregateException or ObjectDisposedException)
        {
            // Nothing more can be done for the terminal; leaving matters more.
        }

        _ender.ExitWithCode(ExitCode);
    }

    public void Dispose()
    {
        _killTimer?.Dispose();
    }

    private void Restore()
    {
        _gate.Close();
        WriteReset();
        _uiThread.Release();
    }

    private void WriteReset()
    {
        try
        {
            if (_reset.CanWriteSequences())
            {
                _output.Write(ResetSequences);
                _output.Flush();
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            // A closed terminal cannot be reset any further.
        }
    }

    private void Kill(object? state)
    {
        _ender.Kill();
    }
}
