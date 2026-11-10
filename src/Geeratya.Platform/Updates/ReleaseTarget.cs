using System.Runtime.InteropServices;
using Geeratya.Platform.Directories;

namespace Geeratya.Platform.Updates;

/// <summary>
/// The release archive made for a system: <c>geeratya-&lt;version&gt;-&lt;Platform&gt;.&lt;extension&gt;</c>.
/// Releases exist for
/// Linux x64 and arm64, Windows x64 and arm64, and macOS on Apple silicon.
/// </summary>
/// <param name="Platform">The platform part of the archive name (<c>linux-x64</c>, <c>osx-arm64</c>, ...).</param>
/// <param name="IsZip">Windows archives are zip files; the others are <c>.tar.gz</c>.</param>
/// <param name="BinaryName">The file inside the archive that is the program.</param>
public sealed record ReleaseTarget(string Platform, bool IsZip, string BinaryName)
{
    /// <summary>The ending of the archive's file name (<c>-linux-x64.tar.gz</c>).</summary>
    public string AssetSuffix => $"-{Platform}{(IsZip ? ".zip" : ".tar.gz")}";

    /// <summary>The target of a system, or null when no release is built for it.</summary>
    public static ReleaseTarget? Of(OperatingSystemKind operatingSystem, Architecture architecture)
    {
        var arch = architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null,
        };

        if (arch is null)
        {
            return null;
        }

        return operatingSystem switch
        {
            OperatingSystemKind.Windows => new ReleaseTarget($"win-{arch}", true, "geeratya.exe"),
            OperatingSystemKind.Linux => new ReleaseTarget($"linux-{arch}", false, "geeratya"),
            _ => arch == "arm64" ? new ReleaseTarget("osx-arm64", false, "geeratya") : null,
        };
    }
}
