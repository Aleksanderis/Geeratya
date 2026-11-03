using Geeratya.Core.Forms;
using Geeratya.Core.Writes;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Transitions.Models;

namespace Geeratya.Core.Sprints;

/// <summary>
/// Where the open issues of a completed sprint go: the backlog (the default) or the next future sprint
/// (<c>sprint.complete</c>, #186). Submitting does not write: it asks the Complete question of
/// <see cref="CompleteSprintRequest"/>, which names the choice.
/// </summary>
public sealed record CompleteSprintFormTarget(long BoardId, Sprint Sprint, SprintTally Tally, Sprint Next) : FormTarget
{
    public const string DestinationId = "destination";
    public const string BacklogOption = "backlog";
    public const string NextOption = "next";

    public override string Title => $"Complete sprint {Sprint.Name}";

    public override string? IssueKey => null;

    public override WriteRequest Request(IReadOnlyList<FieldChange> changes)
    {
        var chosen = changes.FirstOrDefault(change => change.FieldId == DestinationId)?.Value.Chosen;
        var toNext = chosen is [NextOption];

        return new CompleteSprintRequest(BoardId, Sprint, Tally.OpenKeys, Tally.DoneCount, toNext ? Next : null);
    }

    /// <summary>The one choice field, set to the backlog.</summary>
    public IReadOnlyList<FormField> Fields()
    {
        FieldOption[] options =
        [
            new(BacklogOption, "Backlog"),
            new(NextOption, $"Next sprint: {Next.Name}"),
        ];
        var field = FormField.Create(DestinationId, "Open issues go to", FormFieldKind.Select, true, options);

        return [field with { Value = FormValue.OfChosen([BacklogOption]) }];
    }
}
