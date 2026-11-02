using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// The summary and labels of a row changed by an edit. A null "after" leaves that field alone; the undo restores
/// only the fields that still hold the patched value.
/// </summary>
public sealed record EditRowChange(
    string Key,
    string SummaryBefore,
    string? SummaryAfter,
    IReadOnlyList<string> LabelsBefore,
    IReadOnlyList<string>? LabelsAfter) : RowChange(Key)
{
    public override IssueSummary Patch(IssueSummary row)
    {
        return row with { Summary = SummaryAfter ?? row.Summary, Labels = LabelsAfter ?? row.Labels };
    }

    public override IssueSummary Revert(IssueSummary row)
    {
        var summary = SummaryAfter is not null && row.Summary == SummaryAfter ? SummaryBefore : row.Summary;
        var labels = LabelsAfter is not null && row.Labels.SequenceEqual(LabelsAfter) ? LabelsBefore : row.Labels;

        return row with { Summary = summary, Labels = labels };
    }
}
