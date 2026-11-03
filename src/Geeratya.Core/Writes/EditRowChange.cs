using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// The summary, labels, priority and estimate of a row changed by an edit. A null "after" leaves that field alone; the undo
/// restores only the fields that still hold the patched value.
/// </summary>
public sealed record EditRowChange(
    string Key,
    string SummaryBefore,
    string? SummaryAfter,
    IReadOnlyList<string> LabelsBefore,
    IReadOnlyList<string>? LabelsAfter,
    string? PriorityBefore = null,
    string? PriorityAfter = null,
    EstimateEdit? Estimate = null) : RowChange(Key)
{
    public override IssueSummary Patch(IssueSummary row)
    {
        return row with
        {
            Summary = SummaryAfter ?? row.Summary,
            Labels = LabelsAfter ?? row.Labels,
            Priority = PriorityAfter ?? row.Priority,
            Estimate = Estimate is { } edit ? edit.After : row.Estimate,
        };
    }

    public override IssueDetail Patch(IssueDetail detail)
    {
        return detail with
        {
            Summary = SummaryAfter ?? detail.Summary,
            Labels = LabelsAfter ?? detail.Labels,
            Priority = PriorityAfter ?? detail.Priority,
            Estimate = Estimate is { } edit ? edit.After : detail.Estimate,
        };
    }

    public override IssueSummary Revert(IssueSummary row)
    {
        var summary = SummaryAfter is not null && row.Summary == SummaryAfter ? SummaryBefore : row.Summary;
        var labels = LabelsAfter is not null && row.Labels.ToHashSet().SetEquals(LabelsAfter) ? LabelsBefore : row.Labels;

        var priority = PriorityAfter is not null && row.Priority == PriorityAfter ? PriorityBefore : row.Priority;

        var estimate = row.Estimate;

        if (Estimate is { } edit && Nullable.Equals(row.Estimate, edit.After))
        {
            estimate = edit.Before;
        }

        return row with { Summary = summary, Labels = labels, Priority = priority, Estimate = estimate };
    }
}
