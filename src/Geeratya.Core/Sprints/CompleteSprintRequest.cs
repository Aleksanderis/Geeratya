using Geeratya.Core.Writes;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Sprints;

/// <summary>
/// Complete an active sprint and move its open issues to the backlog or to <paramref name="MoveTo"/> (a future
/// sprint). Two Jira calls, complete and then move; the question says so. The open issues are the write's
/// <see cref="Keys"/>, so a second write on one of them is refused while this one runs.
/// </summary>
public sealed record CompleteSprintRequest(
    long BoardId,
    Sprint Sprint,
    IReadOnlyList<string> OpenKeys,
    int DoneCount,
    Sprint? MoveTo) : SprintWriteRequest(BoardId)
{
    public override OperationKind Kind => OperationKind.CompleteSprint;

    public override IReadOnlyList<string> Keys => OpenKeys;

    public override string BusyText => "completing a sprint";

    public override string StartedText => OpenKeys.Count == 0
        ? $"Completing sprint \"{Sprint.Name}\""
        : $"Completing sprint \"{Sprint.Name}\" and moving {Moved} to {Destination}";

    public override string DoneText => OpenKeys.Count == 0
        ? $"Completed sprint \"{Sprint.Name}\""
        : $"Completed sprint \"{Sprint.Name}\", moved {Moved} to {Destination}";

    public override string VerifiedDoneText => OpenKeys.Count == 0
        ? $"Sprint \"{Sprint.Name}\" is completed"
        : $"Sprint \"{Sprint.Name}\" is completed, but its reply was lost, so {Moved} were not moved to "
            + $"{Destination}: move them yourself";

    public override string CheckingText => $"Not sure sprint \"{Sprint.Name}\" was completed, checking";

    public override string NotAppliedText => $"Sprint \"{Sprint.Name}\" was not completed";

    public override string ActionText => $"complete sprint \"{Sprint.Name}\"";

    public override string UnreachableText =>
        $"Sprint \"{Sprint.Name}\" may or may not have been completed: reload to check";

    public override string AskLabel => "Complete sprint";

    /// <summary>The place the open issues go, as the question and the status line name it.</summary>
    public string Destination => MoveTo is { } next ? $"sprint \"{next.Name}\"" : "the backlog";

    /// <summary>Done and open counts, where the open ones go, the two calls, and that Jira has no undo.</summary>
    public override string AskText
    {
        get
        {
            var open = OpenKeys.Count;
            var counts = $"{DoneCount} done, {open} open";

            return $"Complete \"{Sprint.Name}\" ({counts})? {MoveText} Jira cannot undo this.";
        }
    }

    /// <summary>True when the sprint read from Jira is closed.</summary>
    public static bool IsShownBy(Sprint sprint)
    {
        return sprint.State == SprintState.Closed;
    }

    private string Moved => SprintTally.Issues(OpenKeys.Count);

    private string MoveText
    {
        get
        {
            if (OpenKeys.Count == 0)
            {
                return "No open issues to move.";
            }

            return $"The {Moved} will be moved to {Destination} in a second step; "
                + "if that fails they stay in the closed sprint.";
        }
    }
}
