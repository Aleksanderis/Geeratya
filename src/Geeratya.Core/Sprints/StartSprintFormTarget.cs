using Geeratya.Core.Forms;
using Geeratya.Core.Writes;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Sprints;

/// <summary>
/// The form that asks for the dates of a sprint that has none (or only one) before it starts (<c>sprint.start</c>,
/// #186). Submitting it does not write: it asks the Start question of <see cref="StartSprintRequest"/>.
/// </summary>
public sealed record StartSprintFormTarget(long BoardId, Sprint Sprint, SprintTally Tally) : FormTarget
{
    public override string Title => $"Start sprint {Sprint.Name}";

    public override string? IssueKey => null;

    public override WriteRequest Request(IReadOnlyList<FieldChange> changes)
    {
        var start = DateOf(changes, SprintFormFields.StartId);
        var end = DateOf(changes, SprintFormFields.EndId);

        return new StartSprintRequest(BoardId, Sprint, start, end, Tally.Count, Tally.Points, !Tally.IsComplete);
    }

    /// <summary>The two required date fields, filled with the given start and end.</summary>
    public static IReadOnlyList<FormField> Fields(DateTimeOffset start, DateTimeOffset end)
    {
        return
        [
            Prefilled(SprintFormFields.StartId, "Start (yyyy-MM-dd HH:mm)", start),
            Prefilled(SprintFormFields.EndId, "End (yyyy-MM-dd HH:mm)", end),
        ];
    }

    // The value counts as typed (no initial value), so the form submits it as it stands.
    private static FormField Prefilled(string id, string label, DateTimeOffset moment)
    {
        var field = FormField.Create(id, label, FormFieldKind.DateTime, true, []);

        return field with { Value = FormValue.OfText(SprintFormFields.Local(moment) ?? string.Empty) };
    }

    private static DateTimeOffset DateOf(IReadOnlyList<FieldChange> changes, string id)
    {
        var text = changes.First(change => change.FieldId == id).Value.Text;

        return FormValueParser.TryDateTime(text, out var moment) ? moment : default;
    }
}
