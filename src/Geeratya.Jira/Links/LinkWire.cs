namespace Geeratya.Jira.Links;

/// <summary>The JSON shape of <c>GET /issueLinkType</c>. Every member is optional.</summary>
internal static class LinkWire
{
    internal sealed record Types(List<LinkType?>? IssueLinkTypes);

    internal sealed record LinkType(string? Id, string? Name, string? Inward, string? Outward);
}
