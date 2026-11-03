using Geeratya.Core.Views;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// An issue that changes sprint section and lands next to a reference row: the sprint part is
/// <see cref="SprintRowChange"/>, the position part (carried by the first issue of the block) is
/// <see cref="BlockOrderRowChange"/>. The rows are not re-sorted into sections, because the block is placed
/// explicitly next to its reference row, which is already in the target section.
/// </summary>
/// <param name="Key">The moved issue.</param>
/// <param name="Sprint">The sprint part.</param>
/// <param name="Order">The position part, null for every issue but the first of the block.</param>
public sealed record SprintRankRowChange(string Key, SprintRowChange Sprint, BlockOrderRowChange? Order)
    : RowChange(Key)
{
    public override IssueSummary Patch(IssueSummary row)
    {
        return Sprint.Patch(row);
    }

    public override IssueDetail Patch(IssueDetail detail)
    {
        return Sprint.Patch(detail);
    }

    public override IssueSummary Revert(IssueSummary row)
    {
        return Sprint.Revert(row);
    }

    public override IReadOnlyList<IssueSummary> PatchOrder(TabState tab)
    {
        return Order is null ? tab.Rows : Order.PatchOrder(tab);
    }

    public override IReadOnlyList<IssueSummary> RevertOrder(TabState tab)
    {
        return Order is null ? tab.Rows : Order.RevertOrder(tab);
    }
}
