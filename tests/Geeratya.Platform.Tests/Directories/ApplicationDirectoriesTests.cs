using Geeratya.Platform.Directories;

namespace Geeratya.Platform.Tests.Directories;

public class ApplicationDirectoriesTests
{
    private static readonly string _home = Path.Combine(Path.GetTempPath(), "home");
    private static readonly string _roaming = Path.Combine(Path.GetTempPath(), "roaming");
    private static readonly string _local = Path.Combine(Path.GetTempPath(), "local");
    private static readonly PlatformFolders _folders = new(_home, _roaming, _local);

    [Theory]
    [InlineData(OperatingSystemKind.Linux)]
    [InlineData(OperatingSystemKind.MacOs)]
    public void GivenUnixWithoutXdg_WhenResolving_ThenUsesHomeDefaults(OperatingSystemKind operatingSystem)
    {
        var directories = new ApplicationDirectories(operatingSystem, CreateEnvironment([]), _folders);

        Assert.Equal(Path.Combine(_home, ".config", "geeratya"), directories.ConfigDirectory);
        Assert.Equal(Path.Combine(_home, ".local", "state", "geeratya"), directories.StateDirectory);
        Assert.Equal(Path.Combine(_home, ".cache", "geeratya"), directories.CacheDirectory);
    }

    [Fact]
    public void GivenAbsoluteXdgVariables_WhenResolving_ThenTheyWin()
    {
        var xdgRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "xdg"));
        var environment = CreateEnvironment(new Dictionary<string, string>
        {
            ["XDG_CONFIG_HOME"] = Path.Combine(xdgRoot, "config"),
            ["XDG_STATE_HOME"] = Path.Combine(xdgRoot, "state"),
            ["XDG_CACHE_HOME"] = Path.Combine(xdgRoot, "cache"),
        });

        var directories = new ApplicationDirectories(OperatingSystemKind.Linux, environment, _folders);

        Assert.Equal(Path.Combine(xdgRoot, "config", "geeratya"), directories.ConfigDirectory);
        Assert.Equal(Path.Combine(xdgRoot, "state", "geeratya"), directories.StateDirectory);
        Assert.Equal(Path.Combine(xdgRoot, "cache", "geeratya"), directories.CacheDirectory);
    }

    [Fact]
    public void GivenRelativeXdgVariable_WhenResolving_ThenItIsIgnored()
    {
        var environment = CreateEnvironment(new Dictionary<string, string> { ["XDG_CONFIG_HOME"] = "relative/path" });

        var directories = new ApplicationDirectories(OperatingSystemKind.Linux, environment, _folders);

        Assert.Equal(Path.Combine(_home, ".config", "geeratya"), directories.ConfigDirectory);
    }

    [Theory]
    [InlineData(OperatingSystemKind.Linux, "")]
    [InlineData(OperatingSystemKind.MacOs, "")]
    [InlineData(OperatingSystemKind.Linux, "relative")]
    public void GivenUnixWithoutUsableHomeOrXdg_WhenResolving_ThenFailsClearly(
        OperatingSystemKind operatingSystem,
        string home)
    {
        var folders = new PlatformFolders(home, _roaming, _local);

        var error = Assert.Throws<InvalidOperationException>(
            () => new ApplicationDirectories(operatingSystem, CreateEnvironment([]), folders));

        Assert.Contains("HOME", error.Message);
    }

    [Fact]
    public void GivenEmptyHomeButAbsoluteXdgVariables_WhenResolving_ThenXdgIsUsed()
    {
        var xdgRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "xdg"));
        var environment = CreateEnvironment(new Dictionary<string, string>
        {
            ["XDG_CONFIG_HOME"] = xdgRoot,
            ["XDG_STATE_HOME"] = xdgRoot,
            ["XDG_CACHE_HOME"] = xdgRoot,
        });

        var directories = new ApplicationDirectories(
            OperatingSystemKind.Linux, environment, new PlatformFolders("", _roaming, _local));

        Assert.Equal(Path.Combine(xdgRoot, "geeratya"), directories.CacheDirectory);
    }

    [Fact]
    public void GivenWindowsAndNoXdg_WhenResolving_ThenUsesKnownFolders()
    {
        var environment = CreateEnvironment([]);

        var directories = new ApplicationDirectories(OperatingSystemKind.Windows, environment, _folders);

        Assert.Equal(Path.Combine(_roaming, "Geeratya"), directories.ConfigDirectory);
        Assert.Equal(Path.Combine(_local, "Geeratya", "State"), directories.StateDirectory);
        Assert.Equal(Path.Combine(_local, "Geeratya", "Cache"), directories.CacheDirectory);
    }

    [Fact]
    public void GivenWindowsAndAbsoluteXdg_WhenResolving_ThenXdgWinsAndRelativeValuesAreIgnored()
    {
        var environment = CreateEnvironment(new Dictionary<string, string>
        {
            ["XDG_CONFIG_HOME"] = _home,
            ["XDG_STATE_HOME"] = Path.Combine(_home, "state"),
            ["XDG_CACHE_HOME"] = "relative/cache",
        });

        var directories = new ApplicationDirectories(OperatingSystemKind.Windows, environment, _folders);

        Assert.Equal(Path.Combine(_home, "geeratya"), directories.ConfigDirectory);
        Assert.Equal(Path.Combine(_home, "state", "geeratya"), directories.StateDirectory);
        Assert.Equal(Path.Combine(_local, "Geeratya", "Cache"), directories.CacheDirectory);
    }

    private static EnvironmentReader CreateEnvironment(Dictionary<string, string> variables)
    {
        return new EnvironmentReader(name => variables.GetValueOrDefault(name));
    }
}
