using Geeratya.Core.Writes;

namespace Geeratya.Core.Forms;

/// <summary>
/// The fields of one transition screen (required ones first), shown after the transition was picked. Submitting is
/// the confirmation, so the write does not ask again. Optional fields may stay empty.
/// </summary>
public sealed record TransitionFormTarget(string Key, TransitionChoice Choice) : FormTarget
{
    public override string Title => $"Move {Key} to {Choice.Target.Name}";

    public override string? IssueKey => Key;

    public override string EmptyText => $"{Choice.Name} is no longer available for {Key}";

    public override bool AllowsNoChanges => true;

    public override WriteRequest Request(IReadOnlyList<FieldChange> changes)
    {
        return new TransitionRequest(Key, Choice, changes, false);
    }
}
