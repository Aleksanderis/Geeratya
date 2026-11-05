using Geeratya.App.Tests.Fakes;
using Geeratya.Core.Clipboard;
using Geeratya.Core.State;
using Geeratya.Platform.Clipboard;

namespace Geeratya.App.Tests;

public class ClipboardEffectHandlerTests
{
    [Fact]
    public async Task GivenCopyEffect_WhenRunning_ThenTheClipboardGetsTheExactTextAndSuccessIsReported()
    {
        var clipboard = new FakeClipboard(ClipboardResult.Success("wl-copy"));
        var handler = new ClipboardEffectHandler(clipboard);

        var outcome = await handler.RunAsync(new CopyToClipboardEffect("a\nb", "2 lines"), CancellationToken.None);

        Assert.Equal("a\nb", Assert.Single(clipboard.Copied));
        Assert.Equal(new ClipboardCopied("2 lines", true, "wl-copy"), outcome);
    }

    [Fact]
    public async Task GivenClipboardFailure_WhenRunning_ThenTheReasonIsReportedWithoutTheText()
    {
        var clipboard = new FakeClipboard(ClipboardResult.Failure("no clipboard tool found"));
        var handler = new ClipboardEffectHandler(clipboard);

        var outcome = await handler.RunAsync(new CopyToClipboardEffect("secret text", "1 line"), CancellationToken.None);

        var finished = Assert.IsType<ClipboardCopied>(outcome);
        Assert.False(finished.Copied);
        Assert.DoesNotContain("secret", finished.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAClipboardThatThrows_WhenRunning_ThenAFailureIsReportedNotAnException()
    {
        var clipboard = new FakeClipboard(ClipboardResult.Success("x"), new InvalidOperationException("boom with secret text"));
        var handler = new ClipboardEffectHandler(clipboard);

        var outcome = await handler.RunAsync(new CopyToClipboardEffect("secret text", "1 line"), CancellationToken.None);

        var finished = Assert.IsType<ClipboardCopied>(outcome);
        Assert.False(finished.Copied);
        Assert.DoesNotContain("secret", finished.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenOtherEffect_WhenRunning_ThenNothingHappens()
    {
        var clipboard = new FakeClipboard(ClipboardResult.Success("x"));
        var handler = new ClipboardEffectHandler(clipboard);

        var outcome = await handler.RunAsync(new QuitEffect(), CancellationToken.None);

        Assert.Null(outcome);
        Assert.Empty(clipboard.Copied);
        Assert.False(handler.CanRun(new QuitEffect()));
        Assert.True(handler.CanRun(new CopyToClipboardEffect("t", "d")));
    }
}
