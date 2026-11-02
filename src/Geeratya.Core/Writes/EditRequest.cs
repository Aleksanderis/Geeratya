using Geeratya.Core.Forms;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// Change fields of <paramref name="Key"/> (the form's edit target). Jira applies all of it or none, so one shown
/// field proves the whole write. The row patch covers the fields a list row shows (summary, labels, priority; labels
/// are a set, so their order is ignored); the other fields are verified only through those, so a write without any of them
/// cannot be verified by a read: after a lost reply its check ends as "could not check", never as "not applied".
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
        var priority = PriorityOf();

        if (summary is null && labels is null && priority is null)
        {
            return null;
        }

        return new EditRowChange(Key, row.Summary, summary, row.Labels, labels, row.Priority, priority);
    }

    public override bool IsShownBy(IssueSummary row)
    {
        var summary = TextOf(FormFieldIds.Summary);
        var labels = LabelsOf();
        var priority = PriorityOf();

        if (summary is null && labels is null && priority is null)
        {
            return false;
        }

        var summaryShown = summary is null || row.Summary == summary;
        var labelsShown = labels is null || row.Labels.ToHashSet().SetEquals(labels);
        var priorityShown = priority is null || row.Priority == priority;

        return summaryShown && labelsShown && priorityShown;
    }

    private string? TextOf(string fieldId)
    {
        var change = Changes.FirstOrDefault(candidate => candidate.FieldId == fieldId);

        return change is { Kind: FormFieldKind.Text, Value.Text: { Length: > 0 } text } ? text : null;
    }

    // The name of the chosen priority; a cleared priority is not patched (the row keeps what it shows).
    private string? PriorityOf()
    {
        var change = Changes.FirstOrDefault(candidate => candidate.FieldId == FormFieldIds.Priority);

        return change is { Kind: FormFieldKind.Select, Display: [var name] } ? name : null;
    }

    private IReadOnlyList<string>? LabelsOf()
    {
        var change = Changes.FirstOrDefault(candidate => candidate.FieldId == FormFieldIds.Labels);

        return change is { Kind: FormFieldKind.Labels } ? change.Value.Chosen : null;
    }
}
