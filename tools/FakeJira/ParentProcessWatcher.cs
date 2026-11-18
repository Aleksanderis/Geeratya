using System.Diagnostics;

namespace Geeratya.FakeJira;

/// <summary>
/// Ties the server to the script that started it: the script passes its own pid, and the fake exits when that process
/// is gone, also when the script was killed without running any trap (SIGKILL).
/// </summary>
public static class ParentProcessWatcher
{
    /// <summary>True when no process with this id is running (or it has exited).</summary>
    public static bool IsGone(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);

            return process.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    /// <summary>
    /// Blocks until <paramref name="stop"/> is set or, when <paramref name="parentProcessId"/> is above zero, that
    /// process is gone; the parent is checked every <paramref name="interval"/>.
    /// </summary>
    public static void WaitForStopOrParentGone(int parentProcessId, ManualResetEventSlim stop, TimeSpan interval)
    {
        if (parentProcessId <= 0)
        {
            stop.Wait();

            return;
        }

        while (!stop.Wait(interval))
        {
            if (IsGone(parentProcessId))
            {
                return;
            }
        }
    }
}
