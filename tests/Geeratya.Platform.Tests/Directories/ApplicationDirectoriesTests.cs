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
    public void Unix_without_xdg_variables_uses_the_home_defaults(OperatingSystemKind operatingSystem)
    {
        var directories = new ApplicationDirectories(operatingSystem, CreateEnvironment([]), _folders);

        Assert.Equal(Path.Combine(_home, ".config", "geeratya"), directories.ConfigDirectory);
        Assert.Equal(Path.Combine(_home, ".local", "state", "geeratya"), directories.StateDirectory);
        Assert.Equal(Path.Combine(_home, ".cache", "geeratya"), directories.CacheDirectory);
    }

    [Fact]
    public void Absolute_xdg_variables_win()
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
    public void Relative_xdg_variable_is_ignored()
    {
        var environment = CreateEnvironment(new Dictionary<string, string> { ["XDG_CONFIG_HOME"] = "relative/path" });

        var directories = new ApplicationDirectories(OperatingSystemKind.Linux, environment, _folders);

        Assert.Equal(Path.Combine(_home, ".config", "geeratya"), directories.ConfigDirectory);
    }

    [Fact]
    public void Windows_uses_known_folders_and_ignores_xdg()
    {
        var environment = CreateEnvironment(new Dictionary<string, string> { ["XDG_CONFIG_HOME"] = _home });

        var directories = new ApplicationDirectories(OperatingSystemKind.Windows, environment, _folders);

        Assert.Equal(Path.Combine(_roaming, "Geeratya"), directories.ConfigDirectory);
        Assert.Equal(Path.Combine(_local, "Geeratya", "State"), directories.StateDirectory);
        Assert.Equal(Path.Combine(_local, "Geeratya", "Cache"), directories.CacheDirectory);
    }

    private static EnvironmentReader CreateEnvironment(Dictionary<string, string> variables)
    {
        return new EnvironmentReader(name => variables.GetValueOrDefault(name));
    }
}
