using Geeratya.Core.Writes;

namespace Geeratya.Core.Sprints;

/// <summary>
/// Create a future sprint on the board. A lost reply is not checked (the sprint has no id yet), so the status line
/// asks the user to look at the backlog before creating it again.
/// </summary>
public sealed record CreateSprintRequest(
    long BoardId,
    string Name,
    string? Goal,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate) : SprintWriteRequest(BoardId)
{
    public override OperationKind Kind => OperationKind.CreateSprint;

    public override string BusyText => "creating a sprint";

    public override string StartedText => $"Creating sprint \"{Name}\"";

    public override string DoneText => $"Created sprint \"{Name}\"";

    public override string CheckingText => $"Not sure sprint \"{Name}\" was created, checking";

    public override string NotAppliedText => $"Sprint \"{Name}\" was not created";

    public override string ActionText => $"create sprint \"{Name}\"";

    public override string UnreachableText =>
        $"Sprint \"{Name}\" may or may not have been created: reload to check, do not create it twice";
}
