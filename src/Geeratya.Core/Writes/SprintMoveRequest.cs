using System.Globalization;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// Move issues into a sprint, or out of their sprint into the backlog when <paramref name="Target"/> is null (agile
/// API; at most 50 keys per call, longer lists are sent in chunks by the handler). Positional like a rank: no search
/// proves it, so the tabs that show the issues reload afterwards. More than one issue asks first.
/// </summary>
/// <param name="MoveKeys">The issues to move, in view order.</param>
/// <param name="Target">The sprint to move into, null for the backlog.</param>
/// <param name="SourceText">Where the issues are now, for the question (<c>sprint "S1"</c>, <c>the backlog</c>).</param>
public sealed record SprintMoveRequest(IReadOnlyList<string> MoveKeys, IssueSprint? Target, string SourceText)
    : WriteRequest
{
    private const int KeysShown = 8;
    private const string BacklogText = "the backlog";

    public override IReadOnlyList<string> Keys => MoveKeys;

    /// <summary>The target in a sentence: <c>sprint "S2"</c> or <c>the backlog</c>.</summary>
    public string TargetText => Target is null ? BacklogText : $"sprint \"{Target.Name}\"";

    private string Count => Keys.Count.ToString(CultureInfo.InvariantCulture);

    private string Noun => Keys.Count == 1 ? "issue" : "issues";

    private string What => Keys.Count == 1 ? Keys[0] : $"{Count} issues";

    public override OperationKind Kind => OperationKind.SprintMove;

    public override string BusyText => "moving";

    public override string StartedText => $"Moving {What} to {TargetText}";

    public override string DoneText => $"Moved {What} to {TargetText}";

    public override string CheckingText => $"{What}: not sure the move worked, checking";

    public override string NotAppliedText => $"{What}: the move was not applied";

    public override string ActionText => $"move {What} to {TargetText}";

    public override bool ReadsBack => false;

    public override string? AskText => Keys.Count > 1
        ? $"Move {Count} issues ({KeysText()}) from {SourceText} to {TargetText}? Jira has no undo."
        : null;

    public override string AskLabel => $"Move {Noun}";

    public override RowChange? Plan(IssueSummary row)
    {
        return new SprintRowChange(row.Key, row.Sprints, Target);
    }

    public override bool IsShownBy(IssueSummary row)
    {
        return false;
    }

    private string KeysText()
    {
        if (Keys.Count <= KeysShown)
        {
            return string.Join(", ", Keys);
        }

        var more = (Keys.Count - KeysShown).ToString(CultureInfo.InvariantCulture);

        return $"{string.Join(", ", Keys.Take(KeysShown))} and {more} more";
    }
}
