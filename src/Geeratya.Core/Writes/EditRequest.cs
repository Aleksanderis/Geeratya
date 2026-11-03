using Geeratya.Core.Forms;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// Change fields of <paramref name="Key"/> (the form's edit target). Jira applies all of it or none, so one shown
/// field proves the whole write. The row patch covers the fields a list row shows (summary, labels, priority;
/// labels are a set, so their order is ignored); the other fields are verified only through those, so a write without any of them
/// cannot be verified by a read: after a lost reply its check ends as "could not check", never as "not applied".
/// </summary>
public sealed record EditRequest(string Key, IReadOnlyList<FieldChange> Changes, bool EstimateOnly = false)
    : WriteRequest
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
        var estimate = EstimateOf();

        if (summary is null && labels is null && priority is null && estimate is null)
        {
            return null;
        }

        var estimateEdit = estimate is { } after ? new EstimateEdit(row.Estimate, after.Number) : null;

        return new EditRowChange(Key, row.Summary, summary, row.Labels, labels, row.Priority, priority, estimateEdit);
    }

    public override bool IsShownBy(IssueSummary row)
    {
        var summary = TextOf(FormFieldIds.Summary);
        var labels = LabelsOf();
        var priority = PriorityOf();
        var estimate = EstimateOf();

        if (summary is null && labels is null && priority is null && estimate is null)
        {
            return false;
        }

        var summaryShown = summary is null || row.Summary == summary;
        var labelsShown = labels is null || row.Labels.ToHashSet().SetEquals(labels);
        var priorityShown = priority is null || row.Priority == priority;

        var estimateShown = estimate is null || Nullable.Equals(row.Estimate, estimate.Value.Number);

        return summaryShown && labelsShown && priorityShown && estimateShown;
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

    // The estimate form holds one number field; an empty text clears the estimate. Null when this is no estimate edit
    // or the text does not read (the form checks that before it saves).
    private EstimateValue? EstimateOf()
    {
        if (!EstimateOnly || Changes.FirstOrDefault(candidate => candidate.Kind == FormFieldKind.Number) is not { } change)
        {
            return null;
        }

        var text = change.Value.Text.Trim();

        if (text.Length == 0)
        {
            return new EstimateValue(null);
        }

        return FormValueParser.TryNumber(text, out var number) ? new EstimateValue(number) : null;
    }

    private IReadOnlyList<string>? LabelsOf()
    {
        var change = Changes.FirstOrDefault(candidate => candidate.FieldId == FormFieldIds.Labels);

        return change is { Kind: FormFieldKind.Labels } ? change.Value.Chosen : null;
    }

    private readonly record struct EstimateValue(double? Number);
}
