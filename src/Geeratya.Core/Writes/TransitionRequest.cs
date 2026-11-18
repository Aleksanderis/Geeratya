using Geeratya.Core.Forms;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// Move <paramref name="Key"/> through the workflow transition <paramref name="Choice"/>, with the fields of the
/// transition screen in <paramref name="Changes"/> (empty for a plain transition). The row shows the target status
/// at once, which is also how a lost reply is checked. <paramref name="AskFirst"/> makes the reducer confirm before it
/// starts: set for a plain move that may fire workflow side effects, not for a form the user filled and submitted.
/// </summary>
public sealed record TransitionRequest(
    string Key,
    TransitionChoice Choice,
    IReadOnlyList<FieldChange> Changes,
    bool AskFirst,
    string? Summary = null) : WriteRequest
{
    private const string SideEffectWarning = "The workflow may do more than change the status.";

    // The sub-task actions (#994) name the issue by key and summary.
    private string Label => Summary is null ? Key : $"{Key} ({Summary})";

    public override OperationKind Kind => OperationKind.Transition;

    public override IReadOnlyList<string> Keys => [Key];

    public override string BusyText => "changing status";

    public override string StartedText => $"Moving {Label} to {Choice.Target.Name}";

    public override string DoneText => $"Moved {Label} to {Choice.Target.Name}";

    public override string CheckingText => $"{Label}: not sure the move worked, checking";

    public override string NotAppliedText => $"{Key}: the move was not applied";

    public override string ActionText => $"transition {Key}";

    public override string? AskText =>
        AskFirst ? $"Move {Label} to {Choice.Target.Name}? {SideEffectWarning}" : null;

    public override string AskLabel => "Move issue";

    public override string AskHeading => $"Move {Key}";

    public override RowChange? Plan(IssueSummary row)
    {
        return row.Status.Id == Choice.Target.Id ? null : new StatusRowChange(Key, row.Status, Choice.Target);
    }

    public override bool IsShownBy(IssueSummary row)
    {
        return row.Status.Id == Choice.Target.Id;
    }
}
