using System.Globalization;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// Rank a set of issues as one block, directly before or after <paramref name="ReferenceKey"/>, with
/// <paramref name="BlockKeys"/> in the order they have in the view (one call of the rank API). The reference is an issue
/// of the same view that is not part of the block. Like <see cref="RankRequest"/> it is positional, so no row shows
/// it and a re-read proves nothing.
/// </summary>
/// <param name="BlockKeys">The issues to move, in view order.</param>
/// <param name="ReferenceKey">The issue the block goes next to.</param>
/// <param name="Before">Rank above the reference issue, else below it.</param>
/// <param name="RankFieldId">The board's rank field (<c>customfield_NNNNN</c>), null lets Jira pick.</param>
/// <param name="OriginalOrder">The keys of the view before the move, to put the block back on failure.</param>
public sealed record RankBlockRequest(
    IReadOnlyList<string> BlockKeys,
    string ReferenceKey,
    bool Before,
    string? RankFieldId,
    IReadOnlyList<string> OriginalOrder) : WriteRequest
{
    public override IReadOnlyList<string> Keys => BlockKeys;

    private string Count => Keys.Count.ToString(CultureInfo.InvariantCulture);

    private string Place => $"{(Before ? "above" : "below")} {ReferenceKey}";

    private string Noun => Keys.Count == 1 ? "issue" : "issues";

    public override OperationKind Kind => OperationKind.Rank;

    public override string BusyText => "ranking";

    public override string StartedText => $"Ranking {Count} {Noun} {Place}";

    public override string DoneText => $"Ranked {Count} {Noun} {Place}";

    public override string CheckingText => $"{Count} {Noun}: not sure the rank worked, checking";

    public override string NotAppliedText => $"{Count} {Noun}: the rank was not applied";

    public override string ActionText => $"rank {Count} {Noun}";

    public override bool ReadsBack => false;

    public override string? AskText => Keys.Count > 1
        ? $"Rank {Count} issues ({string.Join(", ", Keys)}) {Place}, as one block in this order? Jira has no undo."
        : null;

    public override string AskLabel => "Rank issues";

    /// <summary>One change for the whole block, carried by its first issue.</summary>
    public override RowChange? Plan(IssueSummary row)
    {
        return string.Equals(row.Key, Keys[0], StringComparison.OrdinalIgnoreCase)
            ? new BlockOrderRowChange(Keys, ReferenceKey, Before, OriginalOrder)
            : null;
    }

    public override bool IsShownBy(IssueSummary row)
    {
        return false;
    }
}
