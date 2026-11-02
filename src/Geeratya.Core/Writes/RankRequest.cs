using System.Globalization;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// Rank <paramref name="Key"/> directly before or after <paramref name="ReferenceKey"/>. The reference is an issue of
/// the same view (Jira ranks relative to an issue, never to a position). Rank is positional, so no field of a row
/// shows it: the optimistic change is a move inside the rank-ordered tabs (<see cref="OrderRowChange"/>), and a
/// re-read of the issue cannot tell whether it happened.
/// </summary>
/// <param name="Key">The issue to move.</param>
/// <param name="ReferenceKey">The issue it goes next to.</param>
/// <param name="Before">Rank above the reference issue, else below it.</param>
/// <param name="RankFieldId">The board's rank field (<c>customfield_NNNNN</c>), null lets Jira pick.</param>
/// <param name="WasAfter">The issue above the moved one before the move, to put it back on failure.</param>
/// <param name="WasBefore">The issue below the moved one before the move, to put it back on failure.</param>
/// <param name="HiddenBetween">
/// How many issues that are not shown in the column lie between the two in the board order (cards of other
/// columns); the status line says so, because the card passes them.
/// </param>
public sealed record RankRequest(
    string Key,
    string ReferenceKey,
    bool Before,
    string? RankFieldId,
    string? WasAfter = null,
    string? WasBefore = null,
    int HiddenBetween = 0) : WriteRequest
{
    public override OperationKind Kind => OperationKind.Rank;

    public override IReadOnlyList<string> Keys => [Key];

    public override string BusyText => "ranking";

    public override string StartedText => $"Ranking {Key} {Place}{Hidden}";

    public override string DoneText => $"Ranked {Key} {Place}";

    public override string CheckingText => $"{Key}: not sure the rank worked, checking";

    public override string NotAppliedText => $"{Key}: the rank was not applied";

    public override string ActionText => $"rank {Key}";

    public override bool ReadsBack => false;

    public override RowChange Plan(IssueSummary row)
    {
        return new OrderRowChange(Key, ReferenceKey, Before, WasAfter, WasBefore);
    }

    public override bool IsShownBy(IssueSummary row)
    {
        return false;
    }

    private string Place => $"{(Before ? "above" : "below")} {ReferenceKey}";

    private string Hidden
    {
        get
        {
            if (HiddenBetween == 0)
            {
                return string.Empty;
            }

            if (HiddenBetween == 1)
            {
                return " (1 card of another column lies between)";
            }

            var count = HiddenBetween.ToString(CultureInfo.InvariantCulture);

            return $" ({count} cards of other columns lie between)";
        }
    }
}
