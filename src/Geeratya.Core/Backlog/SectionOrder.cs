namespace Geeratya.Core.Backlog;

/// <summary>Where a section sits: active sprints first, then future ones, then the backlog; by sprint id inside a group.</summary>
/// <param name="Rank">0 active, 1 future, 2 backlog.</param>
/// <param name="Id">The sprint id; 0 for the backlog.</param>
public readonly record struct SectionOrder(int Rank, long Id)
{
    public int CompareTo(SectionOrder other)
    {
        var byRank = Rank.CompareTo(other.Rank);

        return byRank != 0 ? byRank : Id.CompareTo(other.Id);
    }
}
