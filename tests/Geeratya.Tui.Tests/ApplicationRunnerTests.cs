using Geeratya.Core;
using Geeratya.Core.Keymap;

namespace Geeratya.Tui.Tests;

[Trait("Os", "Independent")]
public class ApplicationRunnerTests
{
    [Fact]
    public void GivenVersionFlag_WhenRunning_ThenPrintsNameAndVersion()
    {
        using var writer = new StringWriter();

        var exitCode = new ApplicationRunner(writer, true).Run(["--version"]);

        Assert.Equal(0, exitCode);
        Assert.Equal($"geeratya {ApplicationInfo.Version}{Environment.NewLine}", writer.ToString());
    }

    [Fact]
    public void GivenPrintKeymapFlag_WhenRunning_ThenPrintsTheGeneratedKeyReference()
    {
        using var writer = new StringWriter();

        var exitCode = new ApplicationRunner(writer, false).Run(["--print-keymap-doc"]);

        Assert.Equal(0, exitCode);
        Assert.Equal(KeymapDocument.RenderMarkdown(DefaultKeymap.Load()), writer.ToString());
    }

    [Fact]
    public void GivenNonInteractiveTerminal_WhenRunning_ThenExplainsAndFailsWithoutStartingTheUi()
    {
        using var writer = new StringWriter();

        var exitCode = new ApplicationRunner(writer, false).Run([]);

        Assert.Equal(1, exitCode);
        Assert.Contains("interactive terminal", writer.ToString());
    }

    [Fact]
    public void GivenPrintFrameWithoutOptions_WhenRunning_ThenPrintsAnEightyByTwentyFourFrameWithoutATerminal()
    {
        using var writer = new StringWriter();

        var exitCode = new ApplicationRunner(writer, false).Run(["--print-frame"]);

        var lines = writer.ToString().ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');

        Assert.Equal(0, exitCode);
        Assert.Equal(24, lines.Length);
        Assert.Contains("NORMAL", lines[^1]);
    }

    [Theory]
    [InlineData("--size", "80")]
    [InlineData("--size", "axb")]
    [InlineData("--size", "-1x5")]
    [InlineData("--mode", "insert")]
    public void GivenBadPrintFrameOption_WhenRunning_ThenPrintsUsageAndFails(string flag, string value)
    {
        using var writer = new StringWriter();

        var exitCode = new ApplicationRunner(writer, false).Run(["--print-frame", flag, value]);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage:", writer.ToString());
    }
}
