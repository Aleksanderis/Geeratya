using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Writes;

/// <summary>A command handled by <see cref="WriteReducer"/>.</summary>
public abstract record WriteCommand : Command;

/// <summary>
/// The user asked for a write. Refused with a status line when it touches an issue that is being written; else it
/// starts an operation. <paramref name="At"/> is the time of the command (the reducer reads no clock). A request
/// with a <see cref="WriteRequest.AskText"/> asks first unless <paramref name="Confirmed"/> is set (the command
/// that Confirm mode runs on <c>y</c>).
/// </summary>
public sealed record WriteRequested(TabId Tab, WriteRequest Request, DateTimeOffset At, bool Confirmed = false)
    : WriteCommand;

/// <summary>Assign an issue to the account of the active profile (the pilot write of the pipeline).</summary>
public sealed record AssignToMeRequested(TabId Tab, string Key, DateTimeOffset At) : WriteCommand;

/// <summary>The handler moved an operation to another phase; ignored when the operation is gone.</summary>
public sealed record OperationPhaseChanged(long OperationId, OperationPhase Phase) : WriteCommand;

/// <summary>
/// Jira accepted the write; <paramref name="Rows"/> are the issues read again afterwards (empty when that read
/// failed: the tabs then reload on their own). Applied only while the operation exists. A create carries the key
/// Jira gave the new issue in <paramref name="CreatedKey"/>, which the read may not show yet.
/// <paramref name="Comment"/> is the stored comment of an add or edit of a comment, null for other writes.
/// </summary>
// ReSharper disable NotAccessedPositionalProperty.Global (the time is part of every reply; later issues log it)
public sealed record WriteSucceeded(
    long OperationId,
    DateTimeOffset At,
    IReadOnlyList<IssueSummary> Rows,
    IssueComment? Comment = null,
    string? CreatedKey = null) : WriteCommand;

/// <summary>Jira refused the write or it was not sent: nothing was applied, the optimistic rows are undone.</summary>
public sealed record WriteFailed(long OperationId, DateTimeOffset At, WriteError Error) : WriteCommand;

/// <summary>The reply was lost: the write may or may not have happened, so the issue is read again.</summary>
public sealed record WriteOutcomeUnknown(long OperationId, DateTimeOffset At, WriteError Error) : WriteCommand;

/// <summary>
/// The issue was read again after a lost reply; <paramref name="Comment"/> is the comment the check found for an add
/// or edit of a comment.
/// </summary>
public sealed record WriteVerified(
    long OperationId,
    DateTimeOffset At,
    VerifyOutcome Outcome,
    IReadOnlyList<IssueSummary> Rows,
    IssueComment? Comment = null) : WriteCommand;
