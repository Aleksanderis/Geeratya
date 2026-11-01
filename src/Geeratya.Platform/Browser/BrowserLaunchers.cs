using Geeratya.Platform.Directories;
using Geeratya.Platform.Processes;

namespace Geeratya.Platform.Browser;

/// <summary>Picks the browser launcher for an operating system; every supported system has a branch.</summary>
public static class BrowserLaunchers
{
    public static IBrowserLauncher Create(OperatingSystemKind operatingSystem, IProcessRunner processRunner)
    {
        return new ProcessBrowserLauncher(operatingSystem, processRunner);
    }
}
