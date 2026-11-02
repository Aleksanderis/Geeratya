using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>Assign <paramref name="Key"/> to <paramref name="Assignee"/>, or to nobody when it is null.</summary>
public sealed record AssignRequest(string Key, IssueAssignee? Assignee) : WriteRequest
{
    public override OperationKind Kind => OperationKind.Assign;

    public override IReadOnlyList<string> Keys => [Key];

    public override string BusyText => "assigning";

    public override string StartedText =>
        Assignee is null ? $"Unassigning {Key}" : $"Assigning {Key} to {Assignee.DisplayName}";

    public override string DoneText =>
        Assignee is null ? $"Unassigned {Key}" : $"Assigned {Key} to {Assignee.DisplayName}";

    public override string CheckingText => $"{Key}: not sure the assignment worked, checking";

    public override string NotAppliedText => $"{Key}: the assignment was not applied";

    public override string ActionText => $"assign {Key}";

    // ReSharper disable once ReturnTypeCanBeNotNullable (the base says null for writes that are not optimistic)
    public override RowChange? Plan(IssueSummary row)
    {
        return new AssigneeRowChange(Key, row.Assignee, Assignee);
    }

    public override bool IsShownBy(IssueSummary row)
    {
        return row.Assignee?.AccountId == Assignee?.AccountId;
    }
}
