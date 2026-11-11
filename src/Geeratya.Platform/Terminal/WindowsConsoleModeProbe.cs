using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Geeratya.Platform.Terminal;

/// <summary>The real Windows console: <c>GetConsoleMode</c> on standard output, bit virtual terminal processing.</summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsConsoleModeProbe : IConsoleModeProbe
{
    private const int StandardOutput = -11;
    private const uint VirtualTerminalProcessing = 0x0004;

    public bool OutputSequencesEnabled()
    {
        var handle = GetStdHandle(StandardOutput);

        return handle != IntPtr.Zero
            && handle != new IntPtr(-1)
            && GetConsoleMode(handle, out var mode)
            && (mode & VirtualTerminalProcessing) != 0;
    }

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GetStdHandle(int handle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetConsoleMode(IntPtr handle, out uint mode);
}
