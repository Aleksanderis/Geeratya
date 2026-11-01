using Geeratya.Core;

namespace Geeratya.Tui.Tests;

public class ApplicationRunnerTests
{
    [Fact]
    public void GivenVersionFlag_WhenRunning_ThenPrintsNameAndVersion()
    {
        using var writer = new StringWriter();

        var exitCode = new ApplicationRunner(writer).Run(["--version"]);

        Assert.Equal(0, exitCode);
        Assert.Equal($"geeratya {ApplicationInfo.Version}{Environment.NewLine}", writer.ToString());
    }
}
