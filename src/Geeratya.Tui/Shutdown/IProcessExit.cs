namespace Geeratya.Tui.Shutdown;

/// <summary>Ends the process without the help of the UI thread, which may be stuck (#883).</summary>
public interface IProcessExit
{
    /// <summary>Restores the terminal as far as it can without waiting for long, then ends the process.</summary>
    void EndProcess();
}
