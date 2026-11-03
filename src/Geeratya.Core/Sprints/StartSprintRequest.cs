using Geeratya.Core.Writes;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Sprints;

/// <summary>
/// Start a future sprint with its dates. Asks first (<see cref="AskText"/>): the sprint, how many issues and points
/// it holds and its dates, and that Jira cannot undo it. A lost reply is checked by reading the sprint.
/// </summary>
public sealed record StartSprintRequest(
    long BoardId,
    Sprint Sprint,
    DateTimeOffset Start,
    DateTimeOffset End,
    int IssueCount,
    double Points,
    bool CountIsPartial) : SprintWriteRequest(BoardId)
{
    public override OperationKind Kind => OperationKind.StartSprint;

    public override string BusyText => "starting a sprint";

    public override string StartedText => $"Starting sprint \"{Sprint.Name}\"";

    public override string DoneText => $"Started sprint \"{Sprint.Name}\"";

    public override string CheckingText => $"Not sure sprint \"{Sprint.Name}\" was started, checking";

    public override string NotAppliedText => $"Sprint \"{Sprint.Name}\" was not started";

    public override string ActionText => $"start sprint \"{Sprint.Name}\"";

    public override string UnreachableText =>
        $"Sprint \"{Sprint.Name}\" may or may not have been started: reload to check";

    public override string AskLabel => "Start sprint";

    /// <summary>What happens: the sprint, its issues and points, its dates, and that Jira has no undo.</summary>
    public override string? AskText
    {
        get
        {
            var issues = SprintTally.Issues(IssueCount);
            var counted = CountIsPartial ? $"at least {issues}" : issues;
            var points = Points > 0 ? $", {SprintTally.Number(Points)} pts" : string.Empty;
            var dates = $"{SprintFormFields.Local(Start)} to {SprintFormFields.Local(End)}";

            return $"Start \"{Sprint.Name}\": {counted}{points}, {dates}. Jira cannot undo this.";
        }
    }

    /// <summary>True when the sprint read from Jira is active.</summary>
    public static bool IsShownBy(Sprint sprint)
    {
        return sprint.State == SprintState.Active;
    }
}
