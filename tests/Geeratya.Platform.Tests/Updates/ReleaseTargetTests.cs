using System.Runtime.InteropServices;
using Geeratya.Platform.Directories;
using Geeratya.Platform.Updates;

namespace Geeratya.Platform.Tests.Updates;

[Trait("Os", "Independent")]
public class ReleaseTargetTests
{
    [Theory]
    [InlineData(OperatingSystemKind.Linux, Architecture.X64, "-linux-x64.tar.gz", "geeratya")]
    [InlineData(OperatingSystemKind.Linux, Architecture.Arm64, "-linux-arm64.tar.gz", "geeratya")]
    [InlineData(OperatingSystemKind.Windows, Architecture.X64, "-win-x64.zip", "geeratya.exe")]
    [InlineData(OperatingSystemKind.Windows, Architecture.Arm64, "-win-arm64.zip", "geeratya.exe")]
    [InlineData(OperatingSystemKind.MacOs, Architecture.Arm64, "-osx-arm64.tar.gz", "geeratya")]
    public void GivenASupportedSystem_WhenChoosingTheArchive_ThenItsNameEndsLikeTheReleaseFiles(
        OperatingSystemKind system,
        Architecture architecture,
        string suffix,
        string binary)
    {
        var target = ReleaseTarget.Of(system, architecture)!;

        Assert.Equal(suffix, target.AssetSuffix);
        Assert.Equal(binary, target.BinaryName);
    }

    [Theory]
    [InlineData(OperatingSystemKind.MacOs, Architecture.X64)]
    [InlineData(OperatingSystemKind.Linux, Architecture.Arm)]
    [InlineData(OperatingSystemKind.Windows, Architecture.X86)]
    public void GivenAnUnsupportedSystem_WhenChoosingTheArchive_ThenThereIsNone(
        OperatingSystemKind system,
        Architecture architecture)
    {
        Assert.Null(ReleaseTarget.Of(system, architecture));
    }
}
