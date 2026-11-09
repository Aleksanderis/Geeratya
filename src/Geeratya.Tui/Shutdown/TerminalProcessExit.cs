namespace Geeratya.Tui.Shutdown;

/// <summary>
/// Gives the terminal back and ends the process. The application is disposed and the reset sequences are written on
/// a separate task that is waited on for a second at most: either can block when the terminal or the UI thread is the
/// thing that is stuck, and then the process must still go.
/// </summary>
public sealed class TerminalProcessExit : IProcessExit
{
    /// <summary>Leave the alternate screen, show the cursor, stop mouse and paste reporting, reset attributes.</summary>
    public const string ResetSequences =
        "\e[?1049l\e[?25h\e[?1000l\e[?1002l\e[?1003l\e[?1006l\e[?2004l\e[0m";

    public const int ExitCode = 1;

    private static readonly TimeSpan _restoreWait = TimeSpan.FromSeconds(1);

    private readonly ApplicationUiThread _uiThread;
    private readonly TextWriter _output;

    public TerminalProcessExit(ApplicationUiThread uiThread, TextWriter output)
    {
        _uiThread = uiThread;
        _output = output;
    }

    public void EndProcess()
    {
        try
        {
            Task.Run(Restore).Wait(_restoreWait);
        }
        catch (Exception exception) when (exception is AggregateException or ObjectDisposedException)
        {
            // Nothing more can be done for the terminal; leaving matters more.
        }

        Environment.Exit(ExitCode);
    }

    private void Restore()
    {
        _uiThread.Release();
        _output.Write(ResetSequences);
        _output.Flush();
    }
}
