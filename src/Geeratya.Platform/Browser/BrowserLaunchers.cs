using Geeratya.Platform.Directories;
using Geeratya.Platform.Processes;

namespace Geeratya.Platform.Browser;

/// <summary>
/// Picks the browser launcher for an operating system; the per-system opener (every supported system has a branch,
/// others get a "no opener" message) is chosen in <see cref="ProcessBrowserLauncher.CommandFor"/>.
/// </summary>
public static class BrowserLaunchers
{
    public static IBrowserLauncher Create(OperatingSystemKind operatingSystem, IProcessRunner processRunner)
    {
        return new ProcessBrowserLauncher(operatingSystem, processRunner);
    }
}
