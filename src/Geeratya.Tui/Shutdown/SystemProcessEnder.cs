using System.Diagnostics;

namespace Geeratya.Tui.Shutdown;

/// <summary>The real thing: <c>Environment.Exit</c>, and killing the own process (SIGKILL, TerminateProcess).</summary>
public sealed class SystemProcessEnder : IProcessEnder
{
    public void ExitWithCode(int exitCode)
    {
        Environment.Exit(exitCode);
    }

    public void Kill()
    {
        using var process = Process.GetCurrentProcess();

        process.Kill();
    }
}
