using Geeratya.Core.Detail;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Issues.Models;

namespace Geeratya.Core.Forms;

/// <summary>
/// The <c>&lt;space&gt;t</c> keys of an opened issue: status and assignee of the sub-task under the cursor, while
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

        var target = new TransitionPickTarget(found.Subtask.Key, [], null, [], found.Subtask.Summary);

        return FormReducer.Reduce(state, new OpenFormRequested(found.Tab.Id, target));
    }

    /// <summary><c>issue.subtask-assign</c>: the people picker for the sub-task.</summary>
    public static ReduceResult Assign(AppState state)
    {
        if (Find(state) is not { } found)
        {
            return Say(state, NoSubtaskText);
        }

        var key = found.Subtask.Key;
        var current = found.Tab.Detail[^1].Children
            ?.FirstOrDefault(child => string.Equals(child.Key, key, StringComparison.OrdinalIgnoreCase))?.Assignee;

        return FormReducer.OpenAssign(state, found.Tab.Id, new AssignFormTarget(key, current, found.Subtask.Summary));
    }

    /// <summary><c>issue.subtask-assign-me</c>: assigns the sub-task to the account of the profile.</summary>
    public static ReduceResult AssignMe(AppState state, DateTimeOffset at)
    {
        if (Find(state) is not { } found)
        {
            return Say(state, NoSubtaskText);
        }

        return WriteReducer.Reduce(state, new AssignToMeRequested(found.Tab.Id, found.Subtask.Key, at, found.Subtask.Summary));
    }

    private static Found? Find(AppState state)
    {
        var tab = state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;

        if (tab is null || state.Session.ProfileName is null || tab.Detail is not [.., var shown])
        {
            return null;
        }

        return DetailReducer.SubtaskUnderCursor(shown) is { } subtask ? new Found(tab, subtask) : null;
    }

    private static ReduceResult Say(AppState state, string message)
    {
        return new ReduceResult(state with { Message = message }, _noEffects);
    }

    private sealed record Found(TabState Tab, IssueStub Subtask);
}
