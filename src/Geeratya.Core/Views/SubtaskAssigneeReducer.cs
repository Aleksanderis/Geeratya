using Geeratya.Core.Detail;
using Geeratya.Core.State;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Views;

/// <summary>
/// The assignees of an expanded card's sub-tasks (#911): the board payload's stubs carry none, so expanding a card
/// asks once for <c>parent = KEY</c> and the answer is written into the parent's stubs, which the columns layout
/// lists. Pure.
/// </summary>
public static class SubtaskAssigneeReducer
{
    /// <summary>
    /// The new rows of a refresh with the assignees already read for the expanded cards copied into their fresh stubs
    /// (matched by key), so the chips neither disappear nor cost a request per card on every refresh. The values stay
    /// those of the last expansion until the card is folded and expanded again.
    /// </summary>
    public static IReadOnlyList<IssueSummary> CarryOver(
        IReadOnlyList<IssueSummary> oldRows,
        IReadOnlyList<IssueSummary> newRows,
        IReadOnlyList<string> expanded)
    {
        if (expanded.Count == 0)
        {
            return newRows;
        }

        var known = new Dictionary<string, IssueSubtask>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in oldRows.Where(row => expanded.Contains(row.Key)))
        {
            foreach (var stub in row.Subtasks.Where(static stub => stub.AssigneeKnown))
            {
                known[stub.Key] = stub;
            }
        }

        return known.Count == 0 ? newRows : [.. newRows.Select(row => Carried(row, known))];
    }

    /// <summary>Stores the answer in the stubs of the parent; other stubs and rows are untouched.</summary>
    public static AppState Loaded(AppState state, SubtaskAssigneesLoaded loaded)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, loaded.Tab);
        var index = tab is null ? -1 : IndexOf(tab, loaded.ParentKey);

        if (tab is null || index < 0)
        {
            return state;
        }

        var parent = tab.Rows[index];
        var stubs = parent.Subtasks.Select(stub => With(stub, loaded.Assignees)).ToArray();
        var rows = tab.Rows.ToArray();

        rows[index] = parent with { Subtasks = stubs };

        return ViewReducer.Replace(state, tab with { Rows = rows });
    }

    private static IssueSubtask With(IssueSubtask stub, IReadOnlyList<SubtaskAssignee> assignees)
    {
        var found = assignees.FirstOrDefault(
            each => string.Equals(each.Key, stub.Key, StringComparison.OrdinalIgnoreCase));

        return found is null ? stub : stub with { Assignee = found.Assignee, AssigneeKnown = true };
    }

    private static IssueSummary Carried(IssueSummary row, Dictionary<string, IssueSubtask> known)
    {
        if (row.Subtasks.Count == 0 || !row.Subtasks.Any(stub => known.ContainsKey(stub.Key)))
        {
            return row;
        }

        var stubs = row.Subtasks.Select(stub => Carried(stub, known)).ToArray();

        return row with { Subtasks = stubs };
    }

    private static IssueSubtask Carried(IssueSubtask stub, Dictionary<string, IssueSubtask> known)
    {
        return known.TryGetValue(stub.Key, out var old) ? stub with { Assignee = old.Assignee, AssigneeKnown = true } : stub;
    }

    private static int IndexOf(TabState tab, string key)
    {
        for (var index = 0; index < tab.Rows.Count; index++)
        {
            if (string.Equals(tab.Rows[index].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }
}
