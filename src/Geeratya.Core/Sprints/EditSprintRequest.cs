using Geeratya.Core.Writes;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Sprints;

/// <summary>
/// Change name, goal or dates of a future or active sprint. A lost reply is checked by reading the sprint
/// (<see cref="IsShownBy(Sprint)"/>).
/// </summary>
public sealed record EditSprintRequest(long BoardId, Sprint Before, SprintEdit Edit) : SprintWriteRequest(BoardId)
{
    public override OperationKind Kind => OperationKind.EditSprint;

    /// <summary>The name after the edit.</summary>
    public string Name => Edit.Name ?? Before.Name;

    public override string BusyText => "editing a sprint";

    public override string StartedText => $"Saving sprint \"{Name}\"";

    public override string DoneText => $"Saved sprint \"{Name}\"";

    public override string CheckingText => $"Not sure sprint \"{Name}\" was saved, checking";

    public override string NotAppliedText => $"Sprint \"{Name}\" was not changed";

    public override string ActionText => $"edit sprint \"{Before.Name}\"";

    /// <summary>True when the sprint read from Jira has every value this edit set.</summary>
    public bool IsShownBy(Sprint sprint)
    {
        return (Edit.Name is null || sprint.Name == Edit.Name)
            && (Edit.Goal is null || (sprint.Goal ?? string.Empty) == Edit.Goal)
            && Same(Edit.StartDate, Edit.ClearStartDate, sprint.StartDate)
            && Same(Edit.EndDate, Edit.ClearEndDate, sprint.EndDate);
    }

    private static bool Same(DateTimeOffset? wanted, bool cleared, DateTimeOffset? actual)
    {
        if (cleared)
        {
            return actual is null;
        }

        if (wanted is not { } expected)
        {
            return true;
        }

        return actual is { } found && Seconds(found) == Seconds(expected);
    }

    private static long Seconds(DateTimeOffset moment)
    {
        return moment.UtcDateTime.Ticks / TimeSpan.TicksPerSecond;
    }
}
