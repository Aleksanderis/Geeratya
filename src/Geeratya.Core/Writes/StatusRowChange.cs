using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>The status of a row changed from <paramref name="Before"/> to <paramref name="After"/> (card move).</summary>
public sealed record StatusRowChange(string Key, IssueStatus Before, IssueStatus After) : RowChange(Key)
{
    public override IssueSummary Patch(IssueSummary row)
    {
        return row with { Status = After };
    }

    public override IssueDetail Patch(IssueDetail detail)
    {
        return detail with { Status = After };
    }

    public override IssueSummary Revert(IssueSummary row)
    {
        return row.Status.Id == After.Id ? row with { Status = Before } : row;
    }
}
