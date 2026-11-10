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
    /// <summary>The effects that load the assignees of the expanded cards (a refresh swaps in stubs without assignees).</summary>
    public static IEnumerable<Effect> Loads(TabState tab)
    {
        foreach (var key in tab.ExpandedSubtasks)
        {
            yield return new LoadSubtaskAssigneesEffect(tab.Id, key);
        }
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
        var found = assignees.FirstOrDefault(each => string.Equals(each.Key, stub.Key, StringComparison.OrdinalIgnoreCase));

        return found is null ? stub : stub with { Assignee = found.Assignee, AssigneeKnown = true };
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
