using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>The assignee of a row changed from <paramref name="Before"/> to <paramref name="After"/>.</summary>
public sealed record AssigneeRowChange(string Key, IssueAssignee? Before, IssueAssignee? After) : RowChange(Key)
{
    public override IssueSummary Patch(IssueSummary row)
    {
        return row with { Assignee = After };
    }

    public override IssueSummary Revert(IssueSummary row)
    {
        return row.Assignee == After ? row with { Assignee = Before } : row;
    }
}
