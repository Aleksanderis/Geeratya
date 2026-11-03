using System.Globalization;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// Rank a block of issues next to an issue of another sprint section of the backlog: one command, two calls. The
/// issues first move into the sprint of the reference issue (or into the backlog when <paramref name="Target"/> is
/// null), then rank directly before or after it. Not atomic: when the move worked and the rank did not, the issues
/// stay in the new section and the failure text says so (the tabs reload and show the truth).
/// </summary>
/// <param name="BlockKeys">The issues to move, in view order.</param>
/// <param name="ReferenceKey">The issue the block goes next to; it is in the target section.</param>
/// <param name="Before">Rank above the reference issue, else below it.</param>
/// <param name="RankFieldId">The board's rank field (<c>customfield_NNNNN</c>), null lets Jira pick.</param>
/// <param name="OriginalOrder">The keys of the view before the move, to put the block back on failure.</param>
/// <param name="Target">The sprint the issues move into, null for the backlog.</param>
/// <param name="SourceText">Where the issues are now, for the question.</param>
public sealed record RankMoveRequest(
    IReadOnlyList<string> BlockKeys,
    string ReferenceKey,
    bool Before,
    string? RankFieldId,
    IReadOnlyList<string> OriginalOrder,
    IssueSprint? Target,
    string SourceText) : WriteRequest
{
    /// <summary>Starts the failure text of a rank that failed after the issues had moved.</summary>
    public const string MovedPrefix = "Moved to ";

    private const string BacklogText = "the backlog";
    private const int KeysShown = 8;

    public override OperationKind Kind => OperationKind.RankMove;

    public override IReadOnlyList<string> Keys => BlockKeys;

    /// <summary>The target section in a sentence: <c>sprint "S2"</c> or <c>the backlog</c>.</summary>
    public string TargetText => Target is null ? BacklogText : $"sprint \"{Target.Name}\"";

    private string Count => Keys.Count.ToString(CultureInfo.InvariantCulture);

    private string What => Keys.Count == 1 ? Keys[0] : $"{Count} issues";

    private string Place => $"{(Before ? "above" : "below")} {ReferenceKey}";

    public override string BusyText => "moving and ranking";

    public override string StartedText => $"Moving {What} to {TargetText}, ranked {Place}";

    public override string DoneText => $"Moved {What} to {TargetText}, ranked {Place}";

    public override string CheckingText => $"{What}: not sure the move worked, checking";

    public override string NotAppliedText => $"{What}: the move and rank were not applied";

    public override string ActionText => $"move {What} to {TargetText}";

    public override bool ReadsBack => false;

    public override string? AskText => Keys.Count > 1
        ? $"Move {Count} issues ({KeysText()}) from {SourceText} to {TargetText} and rank them {Place}? " +
          "Two calls, Jira has no undo."
        : null;

    public override string AskLabel => "Move and rank issues";

    /// <summary>One sprint change per issue; the first one also carries the move of the whole block.</summary>
    // ReSharper disable once ReturnTypeCanBeNotNullable (the base says null for writes that are not optimistic)
    public override RowChange? Plan(IssueSummary row)
    {
        var sprint = new SprintRowChange(row.Key, row.Sprints, Target);
        var first = string.Equals(row.Key, Keys[0], StringComparison.OrdinalIgnoreCase);
        var order = first ? new BlockOrderRowChange(Keys, ReferenceKey, Before, OriginalOrder) : null;

        return new SprintRankRowChange(row.Key, sprint, order);
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

        return $"{string.Join(", ", Keys.Take(KeysShown))} and {Keys.Count - KeysShown} more";
    }
}
