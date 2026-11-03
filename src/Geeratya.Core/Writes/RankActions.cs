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
public static class RankActions
{
    private const string NotRankedText = "Ranking works in a board or backlog view";
    private const string InsideIssueText = "Ranking works on a list or a board, not inside an issue";
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

        var others = Enumerable.Range(0, universe.Rows.Count).Where(index => !members.Contains(index)).ToList();

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
        var request = new RankBlockRequest(
            keys,
            universe.Rows[reference].Key,
            before,
            tab.Board?.RankFieldId,
            original);
        var held = tab.Marks.Count > 0 ? tab : tab with { Marks = keys, VisualAnchor = null };
        var kept = ViewReducer.Replace(state, held);

        return WriteReducer.Reduce(kept, new WriteRequested(tab.Id, request, at));
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
                $"Picked up {selected.Key}: move to its new place and press {dropKeys} to drop it (Esc cancels)");
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

    /// <summary>
    /// The card the move lands next to. A card shown under its parent (<c>BoardColumns.Arrange</c>) is part of the
    /// parent's group: moving down passes the whole group of the card it lands on and of the card that moves, and
    /// moving up lands above the parent, because a rank inside a group would not show.
    /// </summary>
    private static int TargetOf(RankUniverse universe, RankMove move, int steps)
    {
        var from = universe.Index;
        var last = universe.Rows.Count - 1;

        switch (move)
        {
            case RankMove.Up:
                return GroupStart(universe, Math.Max(0, from - steps));
            case RankMove.Down:
                var end = GroupEnd(universe, from);

                return end >= last ? from : GroupEnd(universe, Math.Min(last, end + steps));
            case RankMove.Top:
                return 0;
            default:
                return last;
        }
    }

    private static int GroupStart(RankUniverse universe, int index)
    {
        var start = index;

        while (start > 0 && universe.IsNested(start))
        {
            start--;
        }

        return start;
    }

    private static int GroupEnd(RankUniverse universe, int index)
    {
        var end = index;

        while (end + 1 < universe.Rows.Count && universe.IsNested(end + 1))
        {
            end++;
        }

        return end;
    }

    private static ReduceResult Start(
        AppState state,
        TabState tab,
        RankUniverse universe,
        int from,
        int to,
        DateTimeOffset at)
    {
        var request = BuildRequest(tab, universe, from, to);
        var cleared = tab.RankPickup is null ? state : ViewReducer.Replace(state, tab with { RankPickup = null });

        return WriteReducer.Reduce(cleared, new WriteRequested(tab.Id, request, at));
    }

    private static RankRequest BuildRequest(TabState tab, RankUniverse universe, int from, int to)
    {
        var moved = universe.Rows[from];
        var reference = universe.Rows[to];
        var position = IndexOf(tab.Rows, moved.Key);
        var above = position > 0 ? tab.Rows[position - 1].Key : null;
        var below = position >= 0 && position < tab.Rows.Count - 1 ? tab.Rows[position + 1].Key : null;
        var hidden = universe.IsColumn ? CountHidden(tab, universe, moved.Key, reference.Key) : 0;

        return new RankRequest(moved.Key, reference.Key, to < from, tab.Board?.RankFieldId, above, below, hidden);
    }

    /// <summary>The issues of the board order between the two that the column does not show.</summary>
    private static int CountHidden(TabState tab, RankUniverse universe, string movedKey, string referenceKey)
    {
        var first = IndexOf(tab.Rows, movedKey);
        var second = IndexOf(tab.Rows, referenceKey);

        if (first < 0 || second < 0)
        {
            return 0;
        }

        var shown = new HashSet<string>(universe.Rows.Select(row => row.Key), StringComparer.OrdinalIgnoreCase);
        var hidden = 0;

        for (var index = Math.Min(first, second) + 1; index < Math.Max(first, second); index++)
        {
            if (!shown.Contains(tab.Rows[index].Key))
            {
                hidden++;
            }
        }

        return hidden;
    }

    private static TabState? Target(AppState state, out string? refusal)
    {
        refusal = null;

        var tab = state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;

        if (tab is null || tab.Rows.Count == 0)
        {
            refusal = NothingText;

            return null;
        }

        if (tab.Detail.Count > 0)
        {
            refusal = InsideIssueText;

            return null;
        }

        if (!tab.Definition.Source.IsBoardBased())
        {
            refusal = NotRankedText;

            return null;
        }

        return tab;
    }

    private static RankUniverse? UniverseOf(TabState tab)
    {
        if (tab.Definition.Layout == ViewLayout.Columns)
        {
            var column = ColumnsNavigation.SelectedColumn(tab);

            if (column is null || column.Rows.Count == 0)
            {
                return null;
            }

            return new RankUniverse(column.Rows, column.Index, true, column.Nested);
        }

        var index = IndexOf(tab.Rows, tab.SelectedKey);

        var selected = index >= 0 ? index : Math.Clamp(tab.Selected, 0, tab.Rows.Count - 1);

        return new RankUniverse(tab.Rows, selected, false, null);
    }

    private static string EndText(string key, RankMove move, bool partial)
    {
        var top = move is RankMove.Up or RankMove.Top;

        if (top)
        {
            return $"{key} is already at the top";
        }

        return partial ? $"{key} is the last loaded issue: wait for the rest" : $"{key} is already at the bottom";
    }

    private static string Place(TabState tab)
    {
        return tab.Definition.Layout == ViewLayout.Columns ? "column" : "list";
    }

    private static int IndexOf(IReadOnlyList<IssueSummary> rows, string? key)
    {
        if (key is null)
        {
            return -1;
        }

        for (var index = 0; index < rows.Count; index++)
        {
            if (string.Equals(rows[index].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
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
