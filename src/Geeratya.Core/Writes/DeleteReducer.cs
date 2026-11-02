using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// The delete flow up to the confirm: the key counts the sub-tasks of the shown or selected issue (a read, because
/// the answer decides what the confirm says and whether <c>deleteSubtasks</c> is sent), then opens Confirm mode with
/// the write ready to run on <c>y</c>. Nothing is written here. Pure: no clock, no I/O.
/// </summary>
public static class DeleteReducer
{
    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary><c>issue.delete</c>: the issue shown in the detail, else the selected row of the active tab.</summary>
    public static ReduceResult Start(AppState state)
    {
        var session = state.Session;
        var tab = session.Active is { } id ? ViewReducer.Find(session.Tabs, id) : null;

        if (session.ProfileName is null || tab is null)
        {
            return Say(state, "Nothing to delete: open a view and select an issue.");
        }

        if (Target(tab) is not { } target)
        {
            return Say(state, "No issue selected.");
        }

        if (session.Operations.Any(operation => operation.Keys.Contains(target.Key, StringComparer.OrdinalIgnoreCase)))
        {
            return Say(state, $"{target.Key} is busy: wait for the running write to end.");
        }

        var requestId = state.Sequence + 1;
        var pending = new PendingDelete(requestId, tab.Id, target.Key, target.Summary);
        var next = state with
        {
            Sequence = requestId,
            Message = $"Checking {target.Key} for sub-tasks...",
            Session = session with { PendingDelete = pending },
        };

        return new ReduceResult(next, [new CountSubtasksEffect(requestId, target.Key)]);
    }

    /// <summary>
    /// The count arrived: opens the confirm, unless the user has moved on (another mode is open) or the answer is
    /// not for the pending question.
    /// </summary>
    public static ReduceResult Counted(AppState state, SubtasksCounted counted)
    {
        if (state.Session.PendingDelete is not { } pending || pending.RequestId != counted.RequestId)
        {
            return new ReduceResult(state, _noEffects);
        }

        var cleared = state with { Session = state.Session with { PendingDelete = null } };

        if (cleared.Mode != InputMode.Normal)
        {
            return new ReduceResult(cleared with { Message = null }, _noEffects);
        }

        var request = new DeleteIssueRequest(pending.Key, pending.Summary, counted.Count, counted.More);

        // The time of the question stands in for the start of the operation: the reducer reads no clock.
        var confirmed = new WriteRequested(pending.Tab, request, counted.At);
        var confirm = new ConfirmRequest(request.ConfirmText, ActionIds.IssueDelete, pending.Key, confirmed);

        return new ReduceResult(
            cleared with { Mode = InputMode.Confirm, Confirm = confirm, PendingKeys = string.Empty, Message = null },
            _noEffects);
    }

    /// <summary>The count failed: nothing is asked and nothing is deleted.</summary>
    public static ReduceResult CountFailed(AppState state, SubtasksCountFailed failed)
    {
        if (state.Session.PendingDelete is not { } pending || pending.RequestId != failed.RequestId)
        {
            return new ReduceResult(state, _noEffects);
        }

        var text = $"Could not check {pending.Key} for sub-tasks ({failed.Reason}); nothing was deleted";

        return new ReduceResult(
            state with { Message = text, Session = state.Session with { PendingDelete = null } },
            _noEffects);
    }

    private static IssueSummary? RowOf(TabState tab)
    {
        if (tab.Definition.Layout == ViewLayout.Columns)
        {
            return ColumnsNavigation.SelectedIssue(tab);
        }

        return tab.Selected >= 0 && tab.Selected < tab.Rows.Count ? tab.Rows[tab.Selected] : null;
    }

    private static DeleteTarget? Target(TabState tab)
    {
        if (tab.Detail.Count > 0)
        {
            var shown = tab.Detail[^1];

            return new DeleteTarget(shown.Key, shown.Issue?.Summary ?? shown.Row?.Summary ?? string.Empty);
        }

        return RowOf(tab) is { } row ? new DeleteTarget(row.Key, row.Summary) : null;
    }

    private static ReduceResult Say(AppState state, string message)
    {
        return new ReduceResult(state with { Message = message }, _noEffects);
    }

    private sealed record DeleteTarget(string Key, string Summary);
}
