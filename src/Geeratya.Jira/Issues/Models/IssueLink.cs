namespace Geeratya.Jira.Issues.Models;

/// <summary>
/// A link between the opened issue and another one. Reads as "this issue &lt;DirectionText&gt; other", for example
/// GTM-5 blocks GTM-6.
/// </summary>
/// <param name="Id">The link id.</param>
/// <param name="TypeName">The link type name, such as Blocks.</param>
/// <param name="Direction">Which side of the type the opened issue is on.</param>
/// <param name="DirectionText">The type's wording for that side, such as "is blocked by".</param>
/// <param name="Issue">The other issue.</param>
public sealed record IssueLink(
    string Id,
    string TypeName,
    IssueLinkDirection Direction,
    string DirectionText,
    IssueStub Issue);
