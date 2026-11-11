using Geeratya.Platform.Clipboard;

namespace Geeratya.Platform.Tests.Clipboard;

[Trait("Os", "Independent")]
public class NativeWindowsClipboardTests
{
    [Fact]
    public async Task GivenAFreeClipboard_WhenCopying_ThenItIsOpenedEmptiedFilledAndClosedInOrder()
    {
        var api = new ScriptedApi();

        var result = await new NativeWindowsClipboard(api).CopyAsync("héllo", CancellationToken.None);

        Assert.True(result.Copied);
        Assert.Equal(NativeWindowsClipboard.Name, result.Message);
        Assert.Equal(["open", "empty", "set:héllo", "close"], api.Calls);
    }

    [Fact]
    public async Task GivenALongText_WhenCopying_ThenThereIsNoLengthCap()
    {
        var api = new ScriptedApi();
        var text = new string('x', 200_000);

        var result = await new NativeWindowsClipboard(api).CopyAsync(text, CancellationToken.None);

        Assert.True(result.Copied);
        Assert.Contains($"set:{text}", api.Calls);
    }

    [Fact]
    public async Task GivenAClipboardBusyTwice_WhenCopying_ThenOpeningIsRetried()
    {
        var api = new ScriptedApi { OpenFailures = 2 };

        var result = await new NativeWindowsClipboard(api).CopyAsync("text", CancellationToken.None);

        Assert.True(result.Copied);
        Assert.Equal(3, api.Calls.Count(call => call == "open"));
    }

    [Fact]
    public async Task GivenAClipboardThatStaysBusy_WhenCopying_ThenItFailsWithTheErrorAndNeverClosesWhatWasNotOpened()
    {
        var api = new ScriptedApi { OpenFailures = int.MaxValue, LastError = 5 };

        var result = await new NativeWindowsClipboard(api).CopyAsync("secret text", CancellationToken.None);

        Assert.False(result.Copied);
        Assert.Contains("error 5", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("close", api.Calls);
    }

    [Fact]
    public async Task GivenAFailingSet_WhenCopying_ThenTheClipboardIsStillClosed()
    {
        var api = new ScriptedApi { SetSucceeds = false, LastError = 8 };

        var result = await new NativeWindowsClipboard(api).CopyAsync("text", CancellationToken.None);

        Assert.False(result.Copied);
        Assert.Contains("error 8", result.Message, StringComparison.Ordinal);
        Assert.Equal("close", api.Calls[^1]);
    }

    [Fact]
    public async Task GivenAFailingEmpty_WhenCopying_ThenNothingIsSetAndItIsClosed()
    {
        var api = new ScriptedApi { EmptySucceeds = false };

        var result = await new NativeWindowsClipboard(api).CopyAsync("text", CancellationToken.None);

        Assert.False(result.Copied);
        Assert.Equal(["open", "empty", "close"], api.Calls);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenCopying_ThenItThrowsBeforeTouchingTheClipboard()
    {
        var api = new ScriptedApi();
        using var source = new CancellationTokenSource();

        await source.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new NativeWindowsClipboard(api).CopyAsync("text", source.Token));

        Assert.Empty(api.Calls);
    }

    private sealed class ScriptedApi : IWindowsClipboardApi
    {
        public List<string> Calls { get; } = [];

        public int OpenFailures { get; set; }

        public bool EmptySucceeds { get; set; } = true;

        public bool SetSucceeds { get; set; } = true;

        public int LastError { get; set; }

        public bool Open()
        {
            Calls.Add("open");

            if (OpenFailures > 0)
            {
                OpenFailures--;

                return false;
            }

            return true;
        }

        public bool Empty()
        {
            Calls.Add("empty");

            return EmptySucceeds;
        }

        public bool SetUnicodeText(string text)
        {
            Calls.Add($"set:{text}");

            return SetSucceeds;
        }

        public void Close()
        {
            Calls.Add("close");
        }
    }
}
