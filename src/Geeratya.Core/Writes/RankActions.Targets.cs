using Geeratya.Core.Backlog;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Writes;

public static partial class RankActions
{
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
        var request = (WriteRequest?)CrossSections(
                state,
                tab,
                [universe.Rows[from].Key],
                universe.Rows[to],
                to < from,
                tab.Rows.Select(row => row.Key).ToArray())
            ?? BuildRequest(tab, universe, from, to);
        var cleared = tab.RankPickup is null ? state : ViewReducer.Replace(state, tab with { RankPickup = null });

        return WriteReducer.Reduce(cleared, new WriteRequested(tab.Id, request, at));
    }

    /// <summary>
    /// In the Scrum backlog, a move next to a row of another section also changes the sprint of the issues: they take
    /// the sprint of the reference row (the backlog when it has none). Null when every issue is already in that
    /// section (a plain rank) or the tab is not a sectioned backlog.
    /// </summary>
    private static RankMoveRequest? CrossSections(
        AppState state,
        TabState tab,
        IReadOnlyList<string> keys,
        IssueSummary reference,
        bool before,
        IReadOnlyList<string> original)
    {
        if (!BacklogSections.Applies(tab))
        {
            return null;
        }

        var section = BacklogSections.KeyOf(reference);
        var crossing = keys.Any(key => IndexOf(tab.Rows, key) is var index and >= 0 &&
            BacklogSections.KeyOf(tab.Rows[index]) != section);

        if (!crossing)
        {
            return null;
        }

        var sprint = reference.CurrentSprint is { State: SprintState.Active or SprintState.Future } open ? open : null;

        return new RankMoveRequest(
            keys,
            reference.Key,
            before,
            tab.Board?.RankFieldId,
            original,
            sprint,
            SprintMoveActions.SourceOf(state, keys));
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

        if (tab.Marks.Count == 0 && BacklogCursor.OnCollapsedHeader(tab))
        {
            refusal = CollapsedText;

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
}
