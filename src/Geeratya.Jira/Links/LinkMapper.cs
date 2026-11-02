using Geeratya.Jira.Links.Models;

namespace Geeratya.Jira.Links;

/// <summary>Maps the link types. Pure and tolerant: an entry without an id or a name is skipped.</summary>
internal static class LinkMapper
{
    public static IReadOnlyList<IssueLinkType> Map(LinkWire.Types wire)
    {
        var result = new List<IssueLinkType>();

        foreach (var entry in wire.IssueLinkTypes ?? [])
        {
            if (entry is not { Id: { Length: > 0 } id, Name: { Length: > 0 } name })
            {
                continue;
            }

            result.Add(new IssueLinkType(id, name, entry.Inward ?? name, entry.Outward ?? name));
        }

        return result;
    }
}
