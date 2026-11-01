namespace Geeratya.Platform.Browser;

/// <summary>Whether the system opener accepted the URL; <see cref="Message"/> says why not, without the URL.</summary>
public sealed record BrowserLaunchResult(bool Opened, string? Message)
{
    public static BrowserLaunchResult Success { get; } = new(true, null);

    public static BrowserLaunchResult Failure(string message)
    {
        return new BrowserLaunchResult(false, message);
    }
}
