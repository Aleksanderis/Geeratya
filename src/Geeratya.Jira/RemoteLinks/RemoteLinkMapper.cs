using System.Globalization;
using Geeratya.Jira.RemoteLinks.Models;

namespace Geeratya.Jira.RemoteLinks;

/// <summary>
/// Maps the remote link array. Pure and tolerant: an entry with no usable URL is skipped.
/// The scheme is passed through on purpose: the Jira layer reports what Jira holds, and the https-only check runs
/// where a link is opened (<c>BrowserUrlCheck</c> in Geeratya.Core, via <c>OpenUrlRequested</c>).
/// </summary>
internal static class RemoteLinkMapper
{
    public static IReadOnlyList<RemoteLink> Map(List<RemoteLinkWire.Link?>? links)
    {
        var result = new List<RemoteLink>(links?.Count ?? 0);

        foreach (var link in links ?? [])
        {
            if (link?.Object?.Url is not { Length: > 0 } url)
            {
                continue;
            }

            var title = link.Object.Title;

            result.Add(new RemoteLink(
                link.Id?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                url,
                string.IsNullOrWhiteSpace(title) ? url : title,
                link.Application?.Name is { Length: > 0 } name ? name : null));
        }

        return result;
    }
}
