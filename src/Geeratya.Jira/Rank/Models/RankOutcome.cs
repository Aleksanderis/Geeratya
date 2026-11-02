namespace Geeratya.Jira.Rank.Models;

/// <summary>
/// The per-issue results of one rank or backlog call, in request order. A 204 means every issue was moved; a 207
/// lists each issue with its own status, so some may have been moved and some not.
/// </summary>
public sealed record RankOutcome(IReadOnlyList<RankItemResult> Items)
{
    public bool AllRanked => Items.All(item => item.IsRanked);

    public IEnumerable<RankItemResult> Failed => Items.Where(item => !item.IsRanked);
}
