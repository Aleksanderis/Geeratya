using System.Runtime.InteropServices;
using Geeratya.Tui.Shutdown;

namespace Geeratya.Tui;

// Ways out that do not depend on the UI thread (#883): a deadline for every quit, termination signals and a record
// of UI-thread stalls. q and Ctrl-c are keys, handled on the UI thread, so a stuck UI thread never sees them; the
// signals and the deadline are what still end the process then.
public sealed partial class ShellHost
{
    private static readonly TimeSpan _quitGrace = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan _stallThreshold = TimeSpan.FromSeconds(5);

    private static readonly PosixSignal[] _terminationSignals =
        [PosixSignal.SIGINT, PosixSignal.SIGTERM, PosixSignal.SIGHUP];

    private readonly List<PosixSignalRegistration> _signalRegistrations = [];
    private readonly QuitWatchdog _watchdog;
    private UiStallMonitor? _stalls;

    /// <summary>Registers the termination signals and starts the stall monitor, just before the loop runs.</summary>
    private void StartShutdownGuards()
    {
        foreach (var signal in _terminationSignals)
        {
            try
            {
                _signalRegistrations.Add(PosixSignalRegistration.Create(signal, OnTerminationSignal));
            }
            catch (Exception exception) when (exception is PlatformNotSupportedException or ArgumentException)
            {
                // A system without this signal has no other way to send it either.
            }
        }

        if (_profiles.StateDirectory is { } stateDirectory)
        {
            _stalls = new UiStallMonitor(
                TimeProvider.System,
                new FileStallLog(stateDirectory, TimeProvider.System),
                _stallThreshold);

            _stalls.Beat();
            _stalls.Start();
        }
    }

    private void StopShutdownGuards()
    {
        foreach (var registration in _signalRegistrations)
        {
            registration.Dispose();
        }

        _signalRegistrations.Clear();
        _stalls?.Dispose();
    }

    /// <summary>
    /// A termination signal (not the UI thread): the first one stops the loop and gives it a few seconds, the second
    /// ends the process at once. The default action (killing the process without restoring the terminal) is replaced.
    /// </summary>
    private void OnTerminationSignal(PosixSignalContext context)
    {
        context.Cancel = true;

        if (!_watchdog.Terminate(_quitGrace))
        {
            return;
        }

        _quitting = true;
        _application?.Invoke(StopApplication);
    }

    private void StopApplication()
    {
        _application?.RequestStop();
    }
}
