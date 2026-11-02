using Geeratya.Core.Forms;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// Change fields of <paramref name="Key"/> (the form's edit target). Jira applies all of it or none, so one shown
/// field proves the whole write. The row patch covers the fields a list row shows (summary, labels); the other fields
/// are verified only through those, so a write without any of them cannot be verified by a read.
/// </summary>
public sealed record EditRequest(string Key, IReadOnlyList<FieldChange> Changes) : WriteRequest
{
    public override OperationKind Kind => OperationKind.EditFields;

    public override IReadOnlyList<string> Keys => [Key];

    public override string BusyText => "editing";

    public override string StartedText => $"Saving {Key}";

    public override string DoneText => $"Saved {Key}";

    public override string CheckingText => $"{Key}: not sure the edit worked, checking";

    public override string NotAppliedText => $"{Key}: the edit was not applied";

    public override string ActionText => $"edit {Key}";

    public override RowChange? Plan(IssueSummary row)
    {
        var summary = TextOf(FormFieldIds.Summary);
        var labels = LabelsOf();

        if (summary is null && labels is null)
        {
            return null;
        }

        return new EditRowChange(Key, row.Summary, summary, row.Labels, labels);
    }

    public override bool IsShownBy(IssueSummary row)
    {
        var summary = TextOf(FormFieldIds.Summary);
        var labels = LabelsOf();

        if (summary is null && labels is null)
        {
            return false;
        }

        var summaryShown = summary is null || row.Summary == summary;
        var labelsShown = labels is null || row.Labels.SequenceEqual(labels);

        return summaryShown && labelsShown;
    }

    private string? TextOf(string fieldId)
    {
        var change = Changes.FirstOrDefault(candidate => candidate.FieldId == fieldId);

        return change is { Kind: FormFieldKind.Text, Value.Text: { Length: > 0 } text } ? text : null;
    }

    private IReadOnlyList<string>? LabelsOf()
    {
        var change = Changes.FirstOrDefault(candidate => candidate.FieldId == FormFieldIds.Labels);

        return change is { Kind: FormFieldKind.Labels } ? change.Value.Chosen : null;
    }
}
