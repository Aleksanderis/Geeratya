using Geeratya.Platform.Directories;
using Geeratya.Platform.Updates;

namespace Geeratya.Platform.Tests.Updates;

public class InstallRouteDetectorTests
{
    private const string Home = "/home/ann";
    private const string LocalAppData = "C:\\Users\\ann\\AppData\\Local";
    private const string DebianRelease = "NAME=\"Ubuntu\"\nID=ubuntu\nID_LIKE=debian\n";
    private const string ArchRelease = "NAME=\"Arch Linux\"\nID=arch\n";
    private const string FedoraRelease = "ID=fedora\n";

    [Theory]
    [InlineData(OperatingSystemKind.Linux, "/opt/tools/geeratya")]
    [InlineData(OperatingSystemKind.MacOs, "/Users/ann/bin/geeratya")]
    [InlineData(OperatingSystemKind.Windows, "D:\\tools\\geeratya.exe")]
    public void GivenTheScriptMarker_WhenDetecting_ThenTheRouteIsScriptWhereverTheBinarySits(
        OperatingSystemKind system,
        string path)
    {
        var route = InstallRouteDetector.Detect(Facts(system, path, marker: "script\n"));

        Assert.Equal(InstallRouteKind.Script, route.Kind);
        Assert.Equal("run `geeratya update`", route.Instructions);
    }

    [Theory]
    [InlineData(OperatingSystemKind.Linux, "/home/ann/.local/bin/geeratya")]
    [InlineData(OperatingSystemKind.MacOs, "/home/ann/.local/bin/geeratya")]
    [InlineData(OperatingSystemKind.Windows, "C:\\Users\\ann\\AppData\\Local\\Geeratya\\geeratya.exe")]
    public void GivenTheDefaultInstallFolderWithoutAMarker_WhenDetecting_ThenTheRouteIsScript(
        OperatingSystemKind system,
        string path)
    {
        Assert.Equal(InstallRouteKind.Script, InstallRouteDetector.Detect(Facts(system, path)).Kind);
    }

    [Theory]
    [InlineData(OperatingSystemKind.MacOs, "/opt/homebrew/bin/geeratya")]
    [InlineData(OperatingSystemKind.MacOs, "/opt/homebrew/Cellar/geeratya/1.0.0/bin/geeratya")]
    [InlineData(OperatingSystemKind.Linux, "/home/linuxbrew/.linuxbrew/bin/geeratya")]
    public void GivenAHomebrewPath_WhenDetecting_ThenBrewUpgradeIsTheCommand(OperatingSystemKind system, string path)
    {
        var route = InstallRouteDetector.Detect(Facts(system, path));

        Assert.Equal(InstallRouteKind.PackageManager, route.Kind);
        Assert.Contains("brew upgrade geeratya", route.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAWingetPath_WhenDetecting_ThenWingetUpgradeIsTheCommand()
    {
        var path = "C:\\Users\\ann\\AppData\\Local\\Microsoft\\WinGet\\Packages\\Aleksanderis.Geeratya_x\\geeratya.exe";

        var route = InstallRouteDetector.Detect(Facts(OperatingSystemKind.Windows, path));

        Assert.Equal(InstallRouteKind.PackageManager, route.Kind);
        Assert.Contains("winget upgrade Aleksanderis.Geeratya", route.Instructions, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DebianRelease, "sudo apt install")]
    [InlineData(ArchRelease, "sudo pacman -U")]
    [InlineData(FedoraRelease, "sudo dnf install")]
    [InlineData("ID=nixos\n", "your package manager")]
    [InlineData(null, "your package manager")]
    public void GivenAPackageInstalledBinary_WhenDetecting_ThenTheCommandFollowsTheDistribution(
        string? osRelease,
        string expected)
    {
        var route = InstallRouteDetector.Detect(Facts(OperatingSystemKind.Linux, "/usr/bin/geeratya", osRelease));

        Assert.Equal(InstallRouteKind.PackageManager, route.Kind);
        Assert.Contains(expected, route.Instructions, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(OperatingSystemKind.Linux, "/opt/geeratya/geeratya")]
    [InlineData(OperatingSystemKind.MacOs, "/Applications/geeratya")]
    [InlineData(OperatingSystemKind.Windows, "D:\\tools\\geeratya.exe")]
    public void GivenAnUnknownPlace_WhenDetecting_ThenTheRouteIsManualWithTheReleasesPage(
        OperatingSystemKind system,
        string path)
    {
        var route = InstallRouteDetector.Detect(Facts(system, path));

        Assert.Equal(InstallRouteKind.Manual, route.Kind);
        Assert.Contains(InstallRouteDetector.ReleasesPage, route.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAMarkerThatIsNotScript_WhenDetecting_ThenThePathDecides()
    {
        var route = InstallRouteDetector.Detect(Facts(OperatingSystemKind.Linux, "/usr/bin/geeratya", marker: "other"));

        Assert.Equal(InstallRouteKind.PackageManager, route.Kind);
    }

    private static InstallFacts Facts(
        OperatingSystemKind system,
        string path,
        string? osRelease = null,
        string? marker = null)
    {
        return new InstallFacts(system, path, marker, osRelease, Home, LocalAppData);
    }
}
