using Geeratya.Core.Writes;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Links;

/// <summary>
/// A write that creates or removes a link between two issues. Not optimistic: a list row shows nothing of a link,
/// so there is no row to patch, no row to read back and no query that the write could make stale. The open details
/// of both issues reload instead. A lost reply is checked by reading the issue (links are part of it), which does not
/// lag like the search index.
/// </summary>
/// <param name="Key">The issue the user acted on.</param>
/// <param name="OtherKey">The issue at the other end.</param>
/// <param name="Direction">Which wording <paramref name="Key"/> reads: outward ("blocks") or inward.</param>
/// <param name="DirectionText">That wording.</param>
public abstract record LinkWriteRequest(
    string Key,
    string OtherKey,
    IssueLinkDirection Direction,
    string DirectionText) : WriteRequest
{
    public override IReadOnlyList<string> Keys => [Key, OtherKey];

    /// <summary>The link as a sentence: <c>GCM-3 blocks GCM-5</c>.</summary>
    public string Sentence => $"{Key} {DirectionText} {OtherKey}";

    // ReSharper disable once ReturnTypeCanBeNotNullable (the base says null for writes that are not optimistic)
    public override RowChange? Plan(IssueSummary row)
    {
        return null;
    }

    public override bool IsShownBy(IssueSummary row)
    {
        return true;
    }

    /// <summary>True when the read issue shows the change this request intended.</summary>
    public abstract bool IsShownBy(IssueDetail detail);
}
