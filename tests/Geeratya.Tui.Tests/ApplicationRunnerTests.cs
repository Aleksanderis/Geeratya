using Geeratya.Core;
using Geeratya.Core.Keymap;
using Geeratya.Platform.Terminal;

namespace Geeratya.Tui.Tests;

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

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void GivenHelpFlag_WhenRunningWithoutATerminal_ThenPrintsUsageAndSucceeds(string flag)
    {
        using var writer = new StringWriter();

        var exitCode = new ApplicationRunner(writer, false).Run([flag]);

        Assert.Equal(0, exitCode);
        Assert.Contains("--print-frame", writer.ToString());
        Assert.Contains("--version", writer.ToString());
        Assert.DoesNotContain("interactive terminal", writer.ToString());
    }

    [Theory]
    [InlineData("--bogus")]
    [InlineData("GEN-1")]
    public void GivenUnknownArgument_WhenRunning_ThenNamesItPrintsUsageAndExitsTwo(string argument)
    {
        using var writer = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new ApplicationRunner(writer, false, error: error).Run([argument]);

        Assert.Equal(2, exitCode);
        Assert.Contains($"'{argument}'", error.ToString());
        Assert.Contains("Usage:", error.ToString());
        Assert.DoesNotContain("interactive terminal", writer.ToString());
    }

    [Fact]
    public void GivenPrintFrameValueThatLooksLikeAnArgument_WhenRunning_ThenItIsNotReportedUnknown()
    {
        using var writer = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new ApplicationRunner(writer, false, error: error).Run(["--print-frame", "--size", "40x10"]);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
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
    [InlineData("--mode", "visual")]
    public void GivenBadPrintFrameOption_WhenRunning_ThenPrintsUsageAndFails(string flag, string value)
    {
        using var writer = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new ApplicationRunner(writer, false, error: error).Run(["--print-frame", flag, value]);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage:", error.ToString());
        Assert.Equal("", writer.ToString());
    }

    [Fact]
    public void GivenInteractiveTerminal_WhenAskingForHelp_ThenProfilesAreNeverCreated()
    {
        using var writer = new StringWriter();

        var exitCode = new ApplicationRunner(
            writer,
            true,
            createProfiles: () => throw new InvalidOperationException("profiles read")).Run(["--help"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", writer.ToString());
    }

    [Fact]
    public void GivenUnknownArgumentAndHelp_WhenRunning_ThenHelpWins()
    {
        using var writer = new StringWriter();

        var exitCode = new ApplicationRunner(writer, false).Run(["--bogus", "--help"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", writer.ToString());
    }

    [Fact]
    public void GivenNoHomeDirectory_WhenStartingTheShell_ThenPrintsTheMessageToErrorAndFails()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = new ApplicationRunner(
            output,
            true,
            ColorSupport.TrueColor,
            error,
            () => throw new InvalidOperationException("Cannot locate the user's home directory.")).Run([]);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Cannot locate the user's home directory.", error.ToString());
        Assert.Empty(output.ToString());
    }

    [Fact]
    public void GivenNoHomeDirectory_WhenPrintingVersionOrFrame_ThenTheProfileServicesAreNeverCreated()
    {
        using var writer = new StringWriter();
        var runner = new ApplicationRunner(
            writer,
            false,
            ColorSupport.TrueColor,
            TextWriter.Null,
            () => throw new InvalidOperationException("no home"));

        Assert.Equal(0, runner.Run(["--version"]));
        Assert.Equal(0, runner.Run(["--print-frame"]));
    }
}
