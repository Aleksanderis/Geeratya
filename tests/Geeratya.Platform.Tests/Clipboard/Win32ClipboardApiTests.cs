using Geeratya.Platform.Clipboard;

namespace Geeratya.Platform.Tests.Clipboard;

public class Win32ClipboardApiTests
{
    [Fact]
    public async Task GivenWindows_WhenCopyingThroughTheRealApi_ThenTheClipboardAcceptsTheText()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var clipboard = new NativeWindowsClipboard(new Win32ClipboardApi());

        var result = await clipboard.CopyAsync($"geeratya test {Guid.NewGuid():N} 世界", CancellationToken.None);

        Assert.True(result.Copied, result.Message);
    }
}
