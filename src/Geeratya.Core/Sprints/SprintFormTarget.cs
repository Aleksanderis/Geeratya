using Geeratya.Core.Forms;
using Geeratya.Core.Writes;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Sprints;

/// <summary>
/// The form that creates a sprint on a board (<paramref name="Existing"/> is null) or edits one
/// (<c>sprint.create</c>, <c>sprint.edit</c>, #185). It opens at once: there is no metadata to fetch.
/// </summary>
public sealed record SprintFormTarget(long BoardId, Sprint? Existing) : FormTarget
{
    public override string Title => Existing is null ? "Create sprint" : $"Edit sprint {Existing.Name}";

    public override string? IssueKey => null;

    public override WriteRequest Request(IReadOnlyList<FieldChange> changes)
    {
        var name = TextOf(changes, SprintFormFields.NameId);
        var goal = TextOf(changes, SprintFormFields.GoalId);
        var start = DateOf(changes, SprintFormFields.StartId);
        var end = DateOf(changes, SprintFormFields.EndId);

        if (Existing is null)
        {
            return new CreateSprintRequest(BoardId, name ?? string.Empty, goal, start, end);
        }

        // Jira checks the order of the dates only when it gets both, so a change of one sends the other as it is.
        var clearStart = IsCleared(Existing, changes, SprintFormFields.StartId);
        var clearEnd = IsCleared(Existing, changes, SprintFormFields.EndId);
        var datesChanged = start is not null || end is not null || clearStart || clearEnd;
        var edit = new SprintEdit(
            name,
            goal,
            datesChanged && !clearStart ? start ?? Existing.StartDate : null,
            datesChanged && !clearEnd ? end ?? Existing.EndDate : null,
            clearStart,
            clearEnd);

        return new EditSprintRequest(BoardId, Existing, edit);
    }

    private static string? TextOf(IReadOnlyList<FieldChange> changes, string id)
    {
        return changes.FirstOrDefault(change => change.FieldId == id)?.Value.Text.Trim();
    }

    // An emptied date field of an existing sprint removes the date (a replacement, not a partial update).
    private static bool IsCleared(Sprint sprint, IReadOnlyList<FieldChange> changes, string id)
    {
        return TextOf(changes, id) is { Length: 0 } && DateOf(sprint, id) is not null;
    }

    private static DateTimeOffset? DateOf(Sprint sprint, string id)
    {
        return id == SprintFormFields.StartId ? sprint.StartDate : sprint.EndDate;
    }

    private static DateTimeOffset? DateOf(IReadOnlyList<FieldChange> changes, string id)
    {
        var text = TextOf(changes, id);

        return text is not null && FormValueParser.TryDateTime(text, out var moment) ? moment : null;
    }
}
