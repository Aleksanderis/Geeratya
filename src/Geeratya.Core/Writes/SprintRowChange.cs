using Geeratya.Core.Backlog;
using Geeratya.Core.Views;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// An issue moved to <paramref name="Target"/> (null: the backlog): its open sprints (active, future) are replaced by
/// the target, the closed sprints it went through stay. A Scrum backlog tab regroups its rows into the sections.
/// </summary>
/// <param name="Key">The moved issue.</param>
/// <param name="Before">The sprints of the row before the move, put back on failure.</param>
/// <param name="Target">The sprint moved into, null for the backlog.</param>
public sealed record SprintRowChange(string Key, IReadOnlyList<IssueSprint> Before, IssueSprint? Target)
    : RowChange(Key)
{
    public override IssueSummary Patch(IssueSummary row)
    {
        return row with { Sprints = After() };
    }

    public override IssueDetail Patch(IssueDetail detail)
    {
        return detail with { Sprints = After() };
    }

    public override IssueSummary Revert(IssueSummary row)
    {
        return row.Sprints.SequenceEqual(After()) ? row with { Sprints = Before } : row;
    }

    public override IReadOnlyList<IssueSummary> PatchOrder(TabState tab)
    {
        return BacklogSections.Applies(tab) ? BacklogSections.Order(tab.Rows) : tab.Rows;
    }

    public override IReadOnlyList<IssueSummary> RevertOrder(TabState tab)
    {
        return PatchOrder(tab);
    }

    private List<IssueSprint> After()
    {
        var kept = Before.Where(sprint => sprint.State == SprintState.Closed).ToList();

        if (Target is not null)
        {
            kept.Add(Target);
        }

        return kept;
    }
}
