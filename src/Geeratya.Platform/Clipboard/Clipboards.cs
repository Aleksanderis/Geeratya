using Geeratya.Platform.Directories;
using Geeratya.Platform.Processes;

namespace Geeratya.Platform.Clipboard;

/// <summary>
/// Picks the clipboard for an operating system: its own program first, the terminal's OSC 52 sequence as the
/// fallback. Every supported system has a branch (<see cref="ProcessClipboard.CandidatesFor"/>); when neither works
/// the result says so.
/// </summary>
public static class Clipboards
{
    public static IClipboard Create(
        OperatingSystemKind operatingSystem,
        EnvironmentReader environment,
        IProcessRunner processRunner,
        TextWriter? terminal)
    {
        return new ChainedClipboard(
        [
            new ProcessClipboard(operatingSystem, environment, processRunner),
            new Osc52Clipboard(terminal),
        ]);
    }

    /// <summary>
    /// The clipboard of the running system, without the OSC 52 fallback: only the Tui may write to the terminal, so
    /// it builds its own chain with a terminal writer (<see cref="Create"/>).
    /// </summary>
    public static IClipboard ForProcess()
    {
        return Create(
            ApplicationDirectories.DetectOperatingSystem(),
            EnvironmentReader.ForProcess(),
            new ProcessRunner(),
            null);
    }
}
