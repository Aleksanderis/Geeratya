using Geeratya.Core.Writes;
using Geeratya.Jira.Issues.Models;

namespace Geeratya.Core.Links;

/// <summary>
/// Link <paramref name="Key"/> to <paramref name="OtherKey"/>: <paramref name="Key"/> reads
/// <paramref name="DirectionText"/> ("blocks", "is blocked by") of the other. Jira's call names the issue on each side
/// of the type, so the direction decides which key is the outward and which the inward one. <c>TypeId</c> is the
/// link type id.
/// </summary>
public sealed record LinkIssuesRequest(
    string Key,
    string OtherKey,
    string TypeId,
    IssueLinkDirection Direction,
    string DirectionText) : LinkWriteRequest(Key, OtherKey, Direction, DirectionText)
{
    public override OperationKind Kind => OperationKind.LinkIssues;

    public override string BusyText => "linking";

    public override string StartedText => $"Linking {Sentence}";

    public override string DoneText => $"Linked {Sentence}";

    public override string CheckingText => $"{Sentence}: not sure the link was made, checking";

    public override string NotAppliedText => $"{Sentence}: the link was not made";

    public override string ActionText => $"link {Key} to {OtherKey}";

    /// <summary>The issue that reads the type's outward wording ("blocks").</summary>
    public string OutwardKey => Direction == IssueLinkDirection.Outward ? Key : OtherKey;

    /// <summary>The issue that reads the type's inward wording ("is blocked by").</summary>
    public string InwardKey => Direction == IssueLinkDirection.Outward ? OtherKey : Key;

    public override bool IsShownBy(IssueDetail detail)
    {
        return detail.Links.Any(IsThisLink);
    }

    private bool IsThisLink(IssueLink link)
    {
        return link.Direction == Direction
            && link.DirectionText == DirectionText
            && string.Equals(link.Issue.Key, OtherKey, StringComparison.OrdinalIgnoreCase);
    }
}
