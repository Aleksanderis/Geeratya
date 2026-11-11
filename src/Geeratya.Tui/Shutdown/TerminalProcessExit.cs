using Geeratya.Platform.Terminal;

namespace Geeratya.Tui.Shutdown;

/// <summary>
/// Gives the terminal back and ends the process. The reset sequences are written first (while the console is still in
/// the mode the application set up) and the application is disposed after them, on a separate thread that is waited on
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
    private static readonly TimeSpan _writeWait = TimeSpan.FromMilliseconds(400);

    private readonly IApplicationRelease _application;
    private readonly TextWriter _output;
    private readonly UiInvokeGate _gate;
    private readonly TerminalReset _reset;
    private readonly IProcessEnder _ender;
    private readonly TimeProvider _clock;
    private int _started;
    private ITimer? _killTimer;

    public TerminalProcessExit(
        IApplicationRelease application,
        TextWriter output,
        UiInvokeGate gate,
        TerminalReset reset,
        IProcessEnder ender,
        TimeProvider? clock = null)
    {
        _application = application;
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
            // Started with the exit, so the restore wait (up to 1 s) counts against the 2 s.
            _killTimer = _clock.CreateTimer(Kill, null, KillDeadline, Timeout.InfiniteTimeSpan);
        }

        var restorer = new Thread(Restore) { IsBackground = true };

        restorer.Start();
        restorer.Join(_restoreWait);

        _ender.ExitWithCode(ExitCode);
    }

    public void Dispose()
    {
        _killTimer?.Dispose();
    }

    private void Restore()
    {
        _gate.Close();

        // A stuck terminal write must not keep the application from being disposed, so it gets its own short wait, on
        // a thread of its own (a busy or cold thread pool must not eat into that wait).
        var writer = new Thread(WriteReset) { IsBackground = true };

        writer.Start();
        writer.Join(_writeWait);

        try
        {
            _application.Release();
        }
        catch (ObjectDisposedException)
        {
            // Nothing more can be done for the terminal; leaving matters more.
        }
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
