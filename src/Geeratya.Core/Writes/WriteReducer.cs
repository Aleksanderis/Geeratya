using System.Globalization;
using Geeratya.Core.Boards;
using Geeratya.Core.Comments;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// The write part of the reducer (<c>docs/architecture.md</c>, "Write pipeline"): starts operations, refuses a write on
/// an issue that is being written, patches the rows optimistically in every tab, and settles the operation from the
/// reply (success, definite failure with rollback, lost reply with verification). Pure: no clock, no I/O.
/// </summary>
public static partial class WriteReducer
{
    private const string NotConnectedText = "Not connected to Jira: cannot tell which account is yours.";
    private const string ConfirmWriteAction = "write";
    private const string NoProfileText = "No profile is active.";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    public static ReduceResult Reduce(AppState state, WriteCommand command)
    {
        return CommentEditing.Settled(state, ReduceCore(state, command), command);
    }

    private static ReduceResult ReduceCore(AppState state, WriteCommand command)
    {
        return command switch
        {
            AssignToMeRequested me => AssignToMe(state, me),
            WriteRequested requested => Request(state, requested),
            SubtasksCounted counted => DeleteReducer.Counted(state, counted),
            SubtasksCountFailed failed => DeleteReducer.CountFailed(state, failed),
            OperationPhaseChanged changed => PhaseChanged(state, changed),
            WriteSucceeded succeeded => FormReducer.Settled(
                Succeeded(state, succeeded),
                succeeded.OperationId,
                true,
                null),
            WriteFailed failed => FormReducer.Settled(Failed(state, failed), failed.OperationId, false, failed.Error),
            WriteOutcomeUnknown unknown => Unknown(state, unknown.OperationId),
            WriteVerified verified => FormReducer.Settled(
                Verified(state, verified),
                verified.OperationId,
                verified.Outcome == VerifyOutcome.Applied,
                null),
            _ => new ReduceResult(state, _noEffects),
        };
    }

    /// <summary>
    /// The warning for quitting or switching profile while writes run, or null when none does. Their results are
    /// dropped by the generation check, so the user is asked first.
    /// </summary>
    public static string? RunningText(AppState state)
    {
        var count = state.Session.Operations.Count;

        if (count == 0)
        {
            return null;
        }

        if (count == 1)
        {
            return "1 write is still running; its result will not be shown";
        }

        var number = count.ToString(CultureInfo.InvariantCulture);

        return $"{number} writes are still running; their results will not be shown";
    }

    private static ReduceResult AssignToMe(AppState state, AssignToMeRequested request)
    {
        if (state.Session.Connection is not Connected { AccountId: { } accountId } connected)
        {
            return Say(state, NotConnectedText);
        }

        var assignee = new IssueAssignee(accountId, connected.DisplayName);

        return Request(state, new WriteRequested(request.Tab, new AssignRequest(request.Key, assignee), request.At));
    }

    private static ReduceResult Request(AppState state, WriteRequested requested)
    {
        var session = state.Session;
        var write = requested.Request;

        if (session.ProfileName is null)
        {
            return Say(state, NoProfileText);
        }

        if (BusyText(session.Operations, write) is { } busy)
        {
            return Say(state, busy);
        }

        if (!requested.Confirmed && write.AskText is { } question)
        {
            return Ask(state, requested, question);
        }

        var id = state.Sequence + 1;
        var changes = Plan(session.Tabs, write);
        var operation = new Operation(id, write, requested.Tab, OperationPhase.Sending, changes, requested.At);
        var patched = FollowCard(Patch(state, changes), write, requested.Tab);

        var next = patched with
        {
            Sequence = id,
            Message = write.StartedText,
            Session = patched.Session with { Operations = [.. session.Operations, operation] },
        };

        return new ReduceResult(next, [new ExecuteWriteEffect(id, write)]);
    }

    /// <summary>Opens Confirm mode; <c>y</c> runs the same request again, marked as confirmed.</summary>
    private static ReduceResult Ask(AppState state, WriteRequested requested, string question)
    {
        var label = requested.Request.AskLabel;
        var again = requested with { Confirmed = true };
        var confirm = new ConfirmRequest(
            question,
            ConfirmWriteAction,
            null,
            again,
            label,
            Destructive: requested.Request.AskSevere,
            Heading: requested.Request.AskHeading);
        var asking = state with
        {
            Mode = InputMode.Confirm,
            Confirm = confirm,
            PendingKeys = string.Empty,
            Message = null,
        };

        return new ReduceResult(asking, _noEffects);
    }

    /// <summary>
    /// A card that changes status leaves its column: the cursor of the tab that started the move follows the card
    /// into its new column (and back again if the move is rolled back) and keeps the scroll of every column. Only
    /// while the card is still the tab's selection.
    /// </summary>
    private static AppState FollowCard(AppState state, WriteRequest request, TabId tabId)
    {
        if (request.Kind != OperationKind.Transition ||
            request.Keys.Count == 0 ||
            ViewReducer.Find(state.Session.Tabs, tabId) is not { Cursor: not null } tab ||
            !string.Equals(tab.SelectedKey, request.Keys[0], StringComparison.OrdinalIgnoreCase))
        {
            return state;
        }

        return ViewReducer.Replace(state, ColumnsNavigation.FollowCard(tab, request.Keys[0]));
    }

    private static string? BusyText(IReadOnlyList<Operation> operations, WriteRequest write)
    {
        foreach (var operation in operations)
        {
            if (write is SprintWriteRequest sprint
                && operation.Request is SprintWriteRequest running
                && running.BoardId == sprint.BoardId)
            {
                return $"The board is busy ({running.BusyText})";
            }

            foreach (var key in write.Keys)
            {
                if (operation.Keys.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    return $"{key} is busy ({operation.Request.BusyText})";
                }
            }
        }

        return null;
    }

    /// <summary>The optimistic change per key, taken from the first tab that shows the issue.</summary>
    private static List<RowChange> Plan(IReadOnlyList<TabState> tabs, WriteRequest write)
    {
        var changes = new List<RowChange>();

        foreach (var key in write.Keys)
        {
            var row = FindRow(tabs, key);
            var change = row is null ? null : write.Plan(row);

            if (change is not null)
            {
                changes.Add(change);
            }
        }

        return changes;
    }

    private static IssueSummary? FindRow(IReadOnlyList<TabState> tabs, string key)
    {
        foreach (var tab in tabs)
        {
            foreach (var row in tab.Rows)
            {
                if (string.Equals(row.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return row;
                }

                if (row.Subtasks.FirstOrDefault(stub => string.Equals(stub.Key, key, StringComparison.OrdinalIgnoreCase)) is { } found)
                {
                    return BoardColumns.Listed(row, found);
                }
            }
        }

        return null;
    }
}
