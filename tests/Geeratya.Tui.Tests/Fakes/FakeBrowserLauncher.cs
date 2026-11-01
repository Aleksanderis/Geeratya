using Geeratya.Platform.Browser;

namespace Geeratya.Tui.Tests.Fakes;

/// <summary>Records the opened URLs and returns a canned result; no browser starts.</summary>
public sealed class FakeBrowserLauncher : IBrowserLauncher
{
    private readonly BrowserLaunchResult _result;

    public FakeBrowserLauncher(BrowserLaunchResult result)
    {
        _result = result;
    }

    public List<Uri> Opened { get; } = [];

    public Task<BrowserLaunchResult> OpenAsync(Uri url, CancellationToken cancellationToken)
    {
        Opened.Add(url);

        return Task.FromResult(_result);
    }
}
