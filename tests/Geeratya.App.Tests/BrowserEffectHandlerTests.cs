using Geeratya.Core.State;
using Geeratya.Platform.Browser;
using Geeratya.App.Tests.Fakes;

namespace Geeratya.App.Tests;

public class BrowserEffectHandlerTests
{
    private static readonly Uri _url = new("https://work.atlassian.net/browse/ABC-1");

    [Fact]
    public async Task GivenOpenEffect_WhenRunning_ThenLauncherGetsTheExactUrlAndSuccessIsReported()
    {
        var launcher = new FakeBrowserLauncher(BrowserLaunchResult.Success);
        var handler = new BrowserEffectHandler(launcher);

        var outcome = await handler.RunAsync(new OpenUrlEffect(_url), CancellationToken.None);

        Assert.Equal(_url, Assert.Single(launcher.Opened));
        Assert.Null(Assert.IsType<UrlOpenFinished>(outcome).Error);
    }

    [Fact]
    public async Task GivenLauncherFailure_WhenRunning_ThenMessageIsReported()
    {
        var launcher = new FakeBrowserLauncher(BrowserLaunchResult.Failure("no browser opener found: copy the URL"));
        var handler = new BrowserEffectHandler(launcher);

        var outcome = await handler.RunAsync(new OpenUrlEffect(_url), CancellationToken.None);

        Assert.Equal("no browser opener found: copy the URL", Assert.IsType<UrlOpenFinished>(outcome).Error);
    }

    [Fact]
    public async Task GivenLabelledEffect_WhenRunning_ThenTheOutcomeCarriesTheLabel()
    {
        var handler = new BrowserEffectHandler(new FakeBrowserLauncher(BrowserLaunchResult.Success));

        var outcome = await handler.RunAsync(new OpenUrlEffect(_url, "ABC-1"), CancellationToken.None);

        Assert.Equal("ABC-1", Assert.IsType<UrlOpenFinished>(outcome).Label);
    }

    [Fact]
    public async Task GivenOtherEffect_WhenRunning_ThenNothingHappens()
    {
        var launcher = new FakeBrowserLauncher(BrowserLaunchResult.Success);
        var handler = new BrowserEffectHandler(launcher);

        var outcome = await handler.RunAsync(new QuitEffect(), CancellationToken.None);

        Assert.Null(outcome);
        Assert.Empty(launcher.Opened);
    }
}
