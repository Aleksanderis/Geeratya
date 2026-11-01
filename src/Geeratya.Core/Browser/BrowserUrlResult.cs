namespace Geeratya.Core.Browser;

/// <summary>Either a link that may be opened or the reason it may not.</summary>
public sealed record BrowserUrlResult(Uri? Url, string? Reason)
{
    public static BrowserUrlResult Accepted(Uri url)
    {
        return new BrowserUrlResult(url, null);
    }

    public static BrowserUrlResult Refused(string reason)
    {
        return new BrowserUrlResult(null, reason);
    }
}
