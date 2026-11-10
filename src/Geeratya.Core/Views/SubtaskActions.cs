using Geeratya.Core.Detail;
using Geeratya.Core.State;

namespace Geeratya.Core.Views;

/// <summary>
/// <c>section.toggle</c> (<c>z a</c>) in the columns layout: expands or collapses the sub-task list of the card
/// under the cursor (#520). On a row listed from the parent it collapses the parent and moves the cursor to it.
/// </summary>
public static class SubtaskActions
{
    private const string NoSubtasksText = "This card has no sub-tasks.";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary>The toggle; null when the active tab is not in the columns layout, so the caller goes on.</summary>
    public static ReduceResult? Toggle(AppState state)
    {
        var tab = state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;

        if (tab is not { Definition.Layout: ViewLayout.Columns, Detail.Count: 0 }
            || ColumnsNavigation.SelectedIssue(tab) is not { } issue)
        {
            return null;
        }

        if (!issue.IsListed && issue.Subtasks.Count == 0)
        {
            return new ReduceResult(state with { Message = NoSubtasksText }, _noEffects);
        }

        var key = issue.IsListed && issue.Parent is { } parent ? parent.Key : issue.Key;
        var isOpen = tab.ExpandedSubtasks.Contains(key);
        var toggled = tab with { ExpandedSubtasks = Toggled(tab.ExpandedSubtasks, key, isOpen) };

        if (isOpen)
        {
            toggled = ColumnsNavigation.FollowCard(toggled, key);
        }

        var next = ViewReducer.Replace(state, toggled);

        if (isOpen)
        {
            return new ReduceResult(next, _noEffects);
        }

        // Expanding asks once for the sub-tasks' assignees: the board payload's stubs have none (#911).
        return new ReduceResult(next, [new LoadSubtaskAssigneesEffect(tab.Id, key)]);
    }

    private static IReadOnlyList<string> Toggled(IReadOnlyList<string> expanded, string key, bool isOpen)
    {
        return isOpen ? [.. expanded.Where(each => each != key)] : [.. expanded, key];
    }
}
