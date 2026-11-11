using Geeratya.Platform.Directories;

namespace Geeratya.Platform.Terminal;

/// <summary>
/// Decides whether the terminal reset sequences may be written (#940). Linux and macOS terminals always interpret
/// them. On Windows a classic console host can have virtual terminal processing off, and then the escapes would print
/// as literal garbage, so they are written only when the console says it processes them.
/// </summary>
public sealed class TerminalReset
{
    private readonly OperatingSystemKind _operatingSystem;
    private readonly IConsoleModeProbe _probe;

    public TerminalReset(OperatingSystemKind operatingSystem, IConsoleModeProbe probe)
    {
        _operatingSystem = operatingSystem;
        _probe = probe;
    }

    /// <summary>The reset for the running system.</summary>
    public static TerminalReset ForProcess()
    {
        var system = ApplicationDirectories.DetectOperatingSystem();

        return new TerminalReset(
            system,
            OperatingSystem.IsWindows() ? new WindowsConsoleModeProbe() : new AlwaysEnabledProbe());
    }

    /// <summary>Call this while the console is still in the mode the application set up.</summary>
    public bool CanWriteSequences()
    {
        if (_operatingSystem != OperatingSystemKind.Windows)
        {
            return true;
        }

        try
        {
            return _probe.OutputSequencesEnabled();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            return false;
        }
    }

    private sealed class AlwaysEnabledProbe : IConsoleModeProbe
    {
        public bool OutputSequencesEnabled()
        {
            return true;
        }
    }
}
