namespace Geeratya.Tui.Shutdown;

/// <summary>The two ways to end the process: the orderly one and the last resort for when it blocks (#940).</summary>
public interface IProcessEnder
{
    /// <summary>Exits with the given code, running the ProcessExit handlers (which can block).</summary>
    void ExitWithCode(int exitCode);

    /// <summary>Ends the process without running any handler.</summary>
    void Kill();
}
