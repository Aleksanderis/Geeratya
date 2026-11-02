using System.Text.RegularExpressions;

namespace Geeratya.Jira.Issues;

/// <summary>Browser URLs of issues. The one place that builds them; no API call is made.</summary>
public static partial class IssueUrls
{
    private const string BrowsePath = "/browse/";

    /// <summary>
    /// The page of an issue in the browser (<c>https://site/browse/KEY</c>). Null when the key is not a valid
    /// issue key, so nothing a user or a response typed can change the path or query.
    /// </summary>
    public static Uri? Browse(JiraConnection connection, string key)
    {
        if (!IsValidKey(key))
        {
            return null;
        }

        var site = connection.SiteUrl;

        return new Uri($"{site.GetLeftPart(UriPartial.Authority)}{BrowsePath}{key}");
    }

    /// <summary>
    /// True for <c>PROJ-123</c>: an upper-case project key, a hyphen and a number. Lower-case keys, which Jira would
    /// resolve, are not accepted: a caller that takes typed input upper-cases it first.
    /// </summary>
    public static bool IsValidKey(string? key) => key is { Length: > 0 and <= 255 } && KeyPattern().IsMatch(key);

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]*-[0-9]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
