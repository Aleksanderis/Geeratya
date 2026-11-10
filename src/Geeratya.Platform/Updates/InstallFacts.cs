using Geeratya.Platform.Directories;

namespace Geeratya.Platform.Updates;

/// <summary>What the detector needs to know about this install, read by the caller.</summary>
/// <param name="OperatingSystem">The system the binary runs on.</param>
/// <param name="ExecutablePath">The full path of the running binary.</param>
/// <param name="Marker">The text of the marker file next to the binary, null when there is none.</param>
/// <param name="OsRelease">The text of <c>/etc/os-release</c> (Linux), null when unreadable.</param>
/// <param name="Home">The user's home folder.</param>
/// <param name="LocalAppData">The user's local application data folder (the Windows install folder lives here).</param>
public sealed record InstallFacts(
    OperatingSystemKind OperatingSystem,
    string ExecutablePath,
    string? Marker,
    string? OsRelease,
    string Home,
    string LocalAppData);
