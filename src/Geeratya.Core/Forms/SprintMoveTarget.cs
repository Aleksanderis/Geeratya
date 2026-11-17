using System.Globalization;
using Geeratya.Core.Writes;
using Geeratya.Jira.Transitions.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Forms;

/// <summary>
/// The Sprint field of one issue as a one-field form (the Details dropdown): the active and future sprints of the
/// board and the backlog, the current place already chosen. The sprints come from the tab, so nothing is fetched. A
/// pick sends the same <see cref="SprintMoveRequest"/> as the sprint picker of a list key.
/// </summary>
public sealed record SprintMoveTarget(
    string Key,
    IReadOnlyList<Sprint> Sprints,
    IssueSprint? Current,
    string SourceText) : FormTarget
{
    /// <summary>The id of the one field.</summary>
    public const string FieldId = "sprint";

    /// <summary>The option that moves the issue to the backlog.</summary>
    public const string BacklogId = "backlog";

    private const string BacklogLabel = "Backlog";

    public override string Title => $"Sprint of {Key}";

    // ReSharper disable once ReturnTypeCanBeNotNullable (the base allows null for a form with no issue)
    public override string? IssueKey => Key;

    public override bool ClosesOnSend => true;

    /// <summary>The one field: the backlog first, then the sprints, the current place chosen.</summary>
    public FormField Field()
    {
        var options = new List<FieldOption> { new(BacklogId, BacklogLabel) };

        options.AddRange(Sprints.Select(sprint => new FieldOption(SprintId(sprint.Id), sprint.Name)));

        var field = FormField.Create(FieldId, "Sprint", FormFieldKind.Select, true, options);
        var current = Current is { } sprint ? SprintId(sprint.Id) : BacklogId;

        return field.WithInitial(FormValue.OfChosen([current]));
    }

    public override WriteRequest Request(IReadOnlyList<FieldChange> changes)
    {
        var chosen = changes.FirstOrDefault(change => change.FieldId == FieldId)?.Value.Chosen;
        var id = chosen is { Count: > 0 } ? chosen[0] : BacklogId;
        var sprint = Sprints.FirstOrDefault(candidate => SprintId(candidate.Id) == id);
        var target = sprint is null ? null : new IssueSprint(sprint.Id, sprint.Name, sprint.State);

        return new SprintMoveRequest([Key], target, SourceText);
    }

    private static string SprintId(long id)
    {
        return id.ToString(CultureInfo.InvariantCulture);
    }
}
