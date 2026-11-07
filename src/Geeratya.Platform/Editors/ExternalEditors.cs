using Geeratya.Platform.Directories;

namespace Geeratya.Platform.Editors;

/// <summary>Picks the outside editor for the running system.</summary>
public static class ExternalEditors
{
    /// <summary>The editor of this process: Linux, macOS and Windows are supported, any other system is not.</summary>
    public static IExternalEditor ForProcess()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsWindows())
        {
            return new ExternalEditor(
                ApplicationDirectories.DetectOperatingSystem(),
                EnvironmentReader.ForProcess(),
                new TerminalProgramRunner());
        }

        return new UnsupportedExternalEditor();
    }
}
