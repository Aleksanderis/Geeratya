using Geeratya.Core.Selection;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// The rank keys (<c>docs/ux-keymap.md</c>, "Planned bindings"): turn the selection into a <see cref="RankRequest"/>
/// for the active tab, or say why not. Rank is relative to a neighbour that the view shows: one place up or down is
/// the next card of the list or of the board column, to the top or bottom is its first or last card, and the
/// pick-up key takes the place of the card it is dropped on. Pure: the time comes from the command.
/// </summary>
public static partial class RankActions
{
    private const string NotRankedText = "Ranking works in a board or backlog view";

    private const string InsideIssueText = "Ranking works on a list or a board, not inside an issue";

    private const string CollapsedText = "Expand the section first (z a): its issues are hidden";

    private const string NothingText = "Nothing to rank here";

    private const string IncompleteText =
        "Not every issue is loaded yet: wait for the rest before ranking to the bottom";

    private const int MaxBlock = 50;

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary>
    /// Moves the selected issue up or down by <paramref name="count"/> places, or to the top or bottom.
    /// </summary>
    public static ReduceResult Step(AppState state, RankMove move, int count, DateTimeOffset at)
    {
        var tab = Target(state, out var refusal);

        if (tab is null)
        {
            return Say(state, refusal!);
        }

        var universe = UniverseOf(tab);

        if (universe is null)
        {
            return Say(state, NothingText);
        }

        if (RowSelection.HoldsASet(tab))
        {
            return StepBlock(state, tab, universe, move, Math.Max(1, count), at);
        }

        var from = universe.Index;
        var to = TargetOf(universe, move, Math.Max(1, count));

        var partial = tab.Next is not null || tab.Truncated;

        if (move == RankMove.Bottom && partial)
        {
            return Say(state, IncompleteText);
        }

        if (to == from)
        {
            return Say(state, EndText(universe.Rows[from].Key, move, partial));
        }

        return Start(state, tab, universe, from, to, at);
    }

    /// <summary>
    /// A marked set or Visual range moves as one block, order kept: the issues of the set (in view order) are taken
    /// out and put together next to a neighbour that is not part of the set. Up lands before the non-set row that
    /// many places above the first member, down after the one that many places below the last member; a set that is
    /// scattered is gathered by the first step. Top and bottom refuse a set, and so does a set that is not entirely
    /// in the list or column.
    /// </summary>
    private static ReduceResult StepBlock(
        AppState state,
        TabState tab,
        RankUniverse universe,
        RankMove move,
        int steps,
        DateTimeOffset at)
    {
        if (move is RankMove.Top or RankMove.Bottom)
        {
            return Say(state, RowSelection.RefusalText(tab));
        }

        var targets = RowSelection.Targets(tab);
        var members = new List<int>();

        foreach (var key in targets)
        {
            var index = IndexOf(universe.Rows, key);

            if (index >= 0)
            {
                members.Add(index);
            }
        }

        if (members.Count != targets.Count)
        {
            var place = Place(tab);

            return Say(state, $"Not every selected issue is in this {place} or loaded: rank one {place} at a time");
        }

        if (members.Count > MaxBlock)
        {
            return Say(state, $"At most {MaxBlock} issues can be ranked at once: clear some marks");
        }

        members.Sort();

        var others = Enumerable
            .Range(0, universe.Rows.Count)
            .Where(index => !members.Contains(index) && !FollowsMember(universe, members, index))
            .ToList();

        if (others.Count == 0)
        {
            return Say(state, NothingText);
        }

        var before = move == RankMove.Up;
        var reference = before
            ? UpReference(universe, members, others, steps)
            : DownReference(universe, members, others, steps);

        if (IsUnchanged(members, reference, before))
        {
            var edge = before ? "top" : "bottom";

            return Say(state, $"The selected issues are already at the {edge}");
        }

        var keys = members.Select(index => universe.Rows[index].Key).ToArray();
        var original = tab.Rows.Select(row => row.Key).ToArray();
        var request = (WriteRequest?)CrossSections(state, tab, keys, universe.Rows[reference], before, original)
            ?? new RankBlockRequest(keys, universe.Rows[reference].Key, before, tab.Board?.RankFieldId, original);
        var held = tab.Marks.Count > 0 ? tab : tab with { Marks = keys, VisualAnchor = null };
        var kept = ViewReducer.Replace(state, held);

        return WriteReducer.Reduce(kept, new WriteRequested(tab.Id, request, at));
    }

    /// <summary>
    /// True for a card shown under a selected parent: it travels with the parent's group, so it is never the row the
    /// block lands next to (a rank next to it would not show).
    /// </summary>
    private static bool FollowsMember(RankUniverse universe, List<int> members, int index)
    {
        if (!universe.IsNested(index))
        {
            return false;
        }

        return members.Contains(GroupStart(universe, index));
    }

    private static int UpReference(RankUniverse universe, List<int> members, List<int> others, int steps)
    {
        var above = others.Count(index => index < members[0]);
        var reference = others[Math.Max(0, above - steps)];
        var start = GroupStart(universe, reference);

        while (members.Contains(start))
        {
            start++;
        }

        return start;
    }

    private static int DownReference(RankUniverse universe, List<int> members, List<int> others, int steps)
    {
        var passed = others.Count(index => index < members[^1]);
        var reference = others[Math.Min(others.Count - 1, passed - 1 + steps)];
        var end = GroupEnd(universe, reference);

        while (members.Contains(end))
        {
            end--;
        }

        return end;
    }

    /// <summary>True when the block already sits together right next to the reference row.</summary>
    private static bool IsUnchanged(List<int> members, int reference, bool before)
    {
        for (var index = 1; index < members.Count; index++)
        {
            if (members[index] != members[index - 1] + 1)
            {
                return false;
            }
        }

        return before ? members[^1] + 1 == reference : members[0] - 1 == reference;
    }

    /// <summary>
    /// The rank-to key: the first press picks up the selected issue, the next drops it on the issue under the cursor
    /// (above it when the cursor is above the picked-up issue, below it otherwise); on the picked-up issue itself it
    /// cancels.
    /// </summary>
    public static ReduceResult PickUpOrDrop(AppState state, string dropKeys, DateTimeOffset at)
    {
        var tab = Target(state, out var refusal);

        if (tab is null)
        {
            return Say(state, refusal!);
        }

        var universe = UniverseOf(tab);

        if (universe is null)
        {
            return Say(state, NothingText);
        }

        var selected = universe.Rows[universe.Index];

        if (tab.RankPickup is null)
        {
            var picked = ViewReducer.Replace(state, tab with { RankPickup = selected.Key });

            return Say(
                picked,
                $"Picked up {selected.Key}: move to its new place and press {dropKeys} or pick the same right-click entry to drop it (Esc cancels)");
        }

        var from = IndexOf(universe.Rows, tab.RankPickup);
        var dropped = ViewReducer.Replace(state, tab with { RankPickup = null });

        if (from < 0)
        {
            return Say(dropped, $"{tab.RankPickup} is not in this {Place(tab)}: pick it up again");
        }

        if (from == universe.Index)
        {
            return Say(dropped, "Cancelled: nothing to move");
        }

        return Start(dropped, ViewReducer.Find(dropped.Session.Tabs, tab.Id)!, universe, from, universe.Index, at);
    }

    /// <summary>Forgets a picked-up issue of the active tab (Esc); the state itself when none is waiting.</summary>
    public static AppState DropPickup(AppState state)
    {
        var tab = state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;

        return tab is { RankPickup: not null } ? ViewReducer.Replace(state, tab with { RankPickup = null }) : state;
    }

    private static ReduceResult Say(AppState state, string text)
    {
        return new ReduceResult(state with { Message = text }, _noEffects);
    }

    private sealed record RankUniverse(
        IReadOnlyList<IssueSummary> Rows,
        int Index,
        bool IsColumn,
        IReadOnlyList<bool>? Nested)
    {
        public bool IsNested(int index)
        {
            return Nested is not null && index < Nested.Count && Nested[index];
        }
    }
}
