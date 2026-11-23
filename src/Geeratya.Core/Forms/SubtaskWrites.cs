using Geeratya.Core.Detail;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Forms;

/// <summary>
/// The <c>&lt;space&gt;t</c> keys of an opened issue: status and assignee of the sub-task under the cursor (in the
/// columns layout also a sub-task listed under an expanded card, #1054), while
/// <c>&lt;space&gt;i…</c> always acts on the shown issue (#994). Pickers and the status line name the sub-task by key
/// and summary. Pure.
/// </summary>
public static class SubtaskWrites
{
    /// <summary>The status line text when the cursor is not on a sub-task row.</summary>
    public const string NoSubtaskText = "No sub-task under the cursor";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary><c>issue.subtask-transition</c>: the status picker of the sub-task.</summary>
    public static ReduceResult Transition(AppState state)
    {
        if (Find(state) is not { } found)
        {
            return Say(state, NoSubtaskText);
        }

        var target = new TransitionPickTarget(found.Key, [], null, [], found.Summary);

        return FormReducer.Reduce(state, new OpenFormRequested(found.Tab.Id, target));
    }

    /// <summary><c>issue.subtask-assign</c>: the people picker for the sub-task.</summary>
    public static ReduceResult Assign(AppState state)
    {
        if (Find(state) is not { } found)
        {
            return Say(state, NoSubtaskText);
        }

        var target = new AssignFormTarget(found.Key, found.Assignee, found.Summary);

        return FormReducer.OpenAssign(state, found.Tab.Id, target);
    }

    /// <summary><c>issue.subtask-assign-me</c>: assigns the sub-task to the account of the profile.</summary>
    public static ReduceResult AssignMe(AppState state, DateTimeOffset at)
    {
        if (Find(state) is not { } found)
        {
            return Say(state, NoSubtaskText);
        }

        return WriteReducer.Reduce(state, new AssignToMeRequested(found.Tab.Id, found.Key, at, found.Summary));
    }

    private static Found? Find(AppState state)
    {
        var tab = state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;

        if (tab is null || state.Session.ProfileName is null)
        {
            return null;
        }

        if (tab.Detail is [.., var shown])
        {
            return DetailReducer.SubtaskUnderCursor(shown) is { } subtask ? InDetail(tab, shown, subtask) : null;
        }

        // At the view root a sub-task listed under an expanded card of the columns layout is the target (#1054).
        if (tab.Definition.Layout == ViewLayout.Columns
            && ColumnsNavigation.SelectedIssue(tab) is { IsListed: true } listed)
        {
            return new Found(tab, listed.Key, listed.Summary, listed.Assignee);
        }

        return null;
    }

    private static Found InDetail(TabState tab, DetailState shown, IssueStub subtask)
    {
        var current = shown.Children
            ?.FirstOrDefault(child => string.Equals(child.Key, subtask.Key, StringComparison.OrdinalIgnoreCase))
            ?.Assignee;

        return new Found(tab, subtask.Key, subtask.Summary, current);
    }

    private static ReduceResult Say(AppState state, string message)
    {
        return new ReduceResult(state with { Message = message }, _noEffects);
    }

    private sealed record Found(TabState Tab, string Key, string Summary, IssueAssignee? Assignee);
}
