namespace Geeratya.Core.Browser;

/// <summary>
/// Decides whether a link may be handed to the system browser: https only, no control characters, no user info
/// (which could carry a token), and either the host of the active site or, for remote links, any host.
/// </summary>
public static class BrowserUrlCheck
{
    public static BrowserUrlResult Check(string text, string? requiredHost)
    {
        if (text.Any(char.IsControl))
        {
            return BrowserUrlResult.Refused("the link contains control characters");
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
        {
            return BrowserUrlResult.Refused("only https links can be opened");
        }

        if (url.UserInfo.Length > 0)
        {
            return BrowserUrlResult.Refused("the link contains credentials");
        }

        if (requiredHost is not null && !string.Equals(url.Host, requiredHost, StringComparison.OrdinalIgnoreCase))
        {
            return BrowserUrlResult.Refused("the link is not on the active site");
        }

        return BrowserUrlResult.Accepted(url);
    }
}
