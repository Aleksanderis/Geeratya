using Geeratya.Platform.Browser;
using Geeratya.Platform.Directories;
using Geeratya.Platform.Processes;
using Geeratya.Platform.Tests.Secrets;

namespace Geeratya.Platform.Tests.Browser;

[Trait("Os", "Independent")]
public class ProcessBrowserLauncherTests
{
    private static readonly Uri _url = new("https://work.atlassian.net/browse/ABC-1?a=1&b=2");

    [Theory]
    [InlineData(OperatingSystemKind.Linux, "xdg-open", 1)]
    [InlineData(OperatingSystemKind.MacOs, "open", 1)]
    [InlineData(OperatingSystemKind.Windows, "rundll32", 2)]
    public void GivenSystem_WhenBuildingCommand_ThenUrlIsOneArgument(
        OperatingSystemKind system,
        string program,
        int argumentCount)
    {
        var command = ProcessBrowserLauncher.CommandFor(system, _url.AbsoluteUri);

        Assert.NotNull(command);
        Assert.Equal(program, command.Program);
        Assert.Equal(_url.AbsoluteUri, command.Arguments[^1]);
        Assert.Equal(argumentCount, command.Arguments.Count);
    }

    [Theory]
    [InlineData(OperatingSystemKind.Linux)]
    [InlineData(OperatingSystemKind.MacOs)]
    [InlineData(OperatingSystemKind.Windows)]
    public async Task GivenHttpsUrl_WhenOpening_ThenRunnerGetsTheUrlAndSucceeds(OperatingSystemKind system)
    {
        var runner = new FakeProcessRunner(new ProcessResult(0, string.Empty));
        var launcher = new ProcessBrowserLauncher(system, runner);

        var result = await launcher.OpenAsync(_url, CancellationToken.None);

        Assert.True(result.Opened);
        Assert.Equal(_url.AbsoluteUri, Assert.Single(runner.Arguments)[^1]);
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://host/file")]
    [InlineData("mailto:a@b.test")]
    public async Task GivenOtherScheme_WhenOpening_ThenRefusedWithoutStartingAnything(string text)
    {
        var runner = new FakeProcessRunner(new ProcessResult(0, string.Empty));
        var launcher = new ProcessBrowserLauncher(OperatingSystemKind.Linux, runner);

        var result = await launcher.OpenAsync(new Uri(text), CancellationToken.None);

        Assert.False(result.Opened);
        Assert.Empty(runner.Arguments);
    }

    [Fact]
    public async Task GivenMissingOpener_WhenOpening_ThenMessageSaysToCopyTheUrl()
    {
        var launcher = new ProcessBrowserLauncher(OperatingSystemKind.Linux, FakeProcessRunner.Missing());

        var result = await launcher.OpenAsync(_url, CancellationToken.None);

        Assert.False(result.Opened);
        Assert.Equal("no browser opener found: copy the URL", result.Message);
    }

    [Fact]
    public async Task GivenFailingExit_WhenOpening_ThenFailureNamesProgramNotUrl()
    {
        var runner = new FakeProcessRunner(new ProcessResult(3, string.Empty));
        var launcher = new ProcessBrowserLauncher(OperatingSystemKind.MacOs, runner);

        var result = await launcher.OpenAsync(_url, CancellationToken.None);

        Assert.False(result.Opened);
        Assert.Contains("open failed with exit code 3", result.Message);
        Assert.DoesNotContain("atlassian", result.Message);
    }

    [Fact]
    public async Task GivenTimeout_WhenOpening_ThenFailureIsReported()
    {
        var runner = FakeProcessRunner.Failing(new TimeoutException());
        var launcher = new ProcessBrowserLauncher(OperatingSystemKind.Linux, runner);

        var result = await launcher.OpenAsync(_url, CancellationToken.None);

        Assert.False(result.Opened);
    }
}
