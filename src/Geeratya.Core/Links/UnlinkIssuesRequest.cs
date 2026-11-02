using Geeratya.Core.Writes;
using Geeratya.Jira.Issues.Models;

namespace Geeratya.Core.Links;

/// <summary>
/// Remove the link <paramref name="LinkId"/> between <paramref name="Key"/> and <paramref name="OtherKey"/>. Jira has
/// no undo, so the confirm names both issues and the wording of the link. <c>LinkId</c> is the id of the link as
/// the issue lists it.
/// </summary>
public sealed record UnlinkIssuesRequest(
    string Key,
    string OtherKey,
    string LinkId,
    IssueLinkDirection Direction,
    string DirectionText) : LinkWriteRequest(Key, OtherKey, Direction, DirectionText)
{
    public override OperationKind Kind => OperationKind.UnlinkIssues;

    public override string BusyText => "unlinking";

    public override string StartedText => $"Removing the link {Sentence}";

    public override string DoneText => $"Removed the link {Sentence}";

    public override string CheckingText => $"{Sentence}: not sure the link was removed, checking";

    public override string NotAppliedText => $"{Sentence}: the link was not removed";

    public override string ActionText => $"remove the link {Sentence}";

    /// <summary>The question of the confirm: both issues, the wording, and that Jira cannot undo it.</summary>
    public string ConfirmText => $"Remove the link {Sentence}? Jira cannot undo this.";

    public override bool IsShownBy(IssueDetail detail)
    {
        return detail.Links.All(link => link.Id != LinkId);
    }
}
