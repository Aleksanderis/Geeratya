namespace Geeratya.Jira.Links.Models;

/// <summary>
/// A link type the site offers (<c>GET /issueLinkType</c>), read from Jira and never listed in code.
/// </summary>
/// <param name="Id">The type id.</param>
/// <param name="Name">The type name, such as Blocks.</param>
/// <param name="Inward">The wording for the inward side, such as "is blocked by".</param>
/// <param name="Outward">The wording for the outward side, such as "blocks".</param>
public sealed record IssueLinkType(string Id, string Name, string Inward, string Outward);
