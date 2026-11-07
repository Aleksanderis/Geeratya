using System.Globalization;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Sprints;

/// <summary>
/// Creates, edits, starts and completes sprints (agile API). Every method is a write: call it only from a confirmed
/// command, never retry silently, and reload the sprint after an unknown outcome
/// (<see cref="WriteError.IsOutcomeUnknown"/>). The client keeps no cache. Updates, start and complete use
/// <c>POST /sprint/{id}</c>, the partial update: <c>PUT</c> replaces the sprint and drops an omitted goal.
/// <see cref="StartAsync"/> and <see cref="CompleteAsync"/> change the workflow of a team (the UI confirms them
/// first, #186). Jira allows several active sprints at once, so there is no
/// "another sprint is active" error. Completing does not move unfinished issues: <c>moveIssuesTo</c> is rejected with
/// 400, so they stay in the closed sprint and show up in the backlog; moving them is a second call (open question 8
/// in docs/internal/roadmap.md). Only call these on a Scrum board's sprints (a Kanban board has none).
/// </summary>
public sealed class SprintWriteClient(JiraTransport transport)
{
    private const string SprintPath = "/rest/agile/1.0/sprint";

    /// <summary>Creates a future sprint on the board. Dates are optional here; starting needs both.</summary>
    public Task<WriteResult<WrittenSprint>> CreateAsync(
        string name,
        long originBoardId,
        string? goal,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Refused(SprintWriteFields.Name, SprintWriteErrors.NameMissingMessage);
        }

        if (originBoardId <= 0)
        {
            return Refused(null, "A valid board id is required.");
        }

        if (CheckDates(startDate, endDate) is { } refused)
        {
            return Task.FromResult(refused);
        }

        var body = SprintWriteBody.Create(name.Trim(), originBoardId, goal, startDate, endDate);

        return SendAsync(HttpMethod.Post, SprintPath, body, cancellationToken);
    }

    /// <summary>Changes the members of <paramref name="edit"/> that are set; everything else stays as it is.</summary>
    public Task<WriteResult<WrittenSprint>> UpdateAsync(
        long sprintId,
        SprintEdit edit,
        CancellationToken cancellationToken)
    {
        if (!IsValidId(sprintId))
        {
            return Refused(null, "A valid sprint id is required.");
        }

        if (edit.IsEmpty)
        {
            return Refused(null, "Nothing to change.");
        }

        if (edit.ClearsDate)
        {
            return Refused(null, "The partial update cannot remove a date: use ReplaceAsync.");
        }

        if (edit.Name is not null && string.IsNullOrWhiteSpace(edit.Name))
        {
            return Refused(SprintWriteFields.Name, SprintWriteErrors.NameMissingMessage);
        }

        if (CheckDates(edit.StartDate, edit.EndDate) is { } refused)
        {
            return Task.FromResult(refused);
        }

        var body = SprintWriteBody.Update(edit.Name?.Trim(), edit.Goal, edit.StartDate, edit.EndDate);

        return SendAsync(HttpMethod.Post, SprintPathFor(sprintId), body, cancellationToken);
    }

    /// <summary>
    /// An edit that removes a date. The partial update ignores <c>null</c>, so this sends the whole sprint with
    /// <c>PUT</c> (checked on the dev site): <paramref name="current"/> supplies every value the edit leaves alone,
    /// because <c>PUT</c> drops what is omitted. Edits without a cleared date use <see cref="UpdateAsync"/>.
    /// </summary>
    public Task<WriteResult<WrittenSprint>> ReplaceAsync(
        Sprint current,
        SprintEdit edit,
        CancellationToken cancellationToken)
    {
        if (!IsValidId(current.Id))
        {
            return Refused(null, "A valid sprint id is required.");
        }

        var name = (edit.Name ?? current.Name).Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return Refused(SprintWriteFields.Name, SprintWriteErrors.NameMissingMessage);
        }

        var goal = edit.Goal ?? current.Goal;
        var start = edit.ClearStartDate ? null : edit.StartDate ?? current.StartDate;
        var end = edit.ClearEndDate ? null : edit.EndDate ?? current.EndDate;

        if (CheckDates(start, end) is { } refused)
        {
            return Task.FromResult(refused);
        }

        if (StateName(current.State) is not { } state)
        {
            return Refused(null, "The state of the sprint is unknown, so it is not replaced.");
        }

        var kept = string.IsNullOrEmpty(goal) ? null : goal;
        var body = SprintWriteBody.Replace(name, state, kept, start, end);

        return SendAsync(HttpMethod.Put, SprintPathFor(current.Id), body, cancellationToken);
    }

    /// <summary>
    /// Starts a future sprint with the given dates (<c>future</c> to <c>active</c>). Starting a closed one is
    /// refused (<see cref="SprintWriteProblem.AlreadyClosed"/>).
    /// </summary>
    public Task<WriteResult<WrittenSprint>> StartAsync(
        long sprintId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken)
    {
        if (!IsValidId(sprintId))
        {
            return Refused(null, "A valid sprint id is required.");
        }

        if (CheckDates(startDate, endDate) is { } refused)
        {
            return Task.FromResult(refused);
        }

        return SendAsync(
            HttpMethod.Post,
            SprintPathFor(sprintId),
            SprintWriteBody.Start(startDate, endDate),
            cancellationToken);
    }

    /// <summary>
    /// Completes an active sprint (<c>active</c> to <c>closed</c>). Unfinished issues stay in the closed sprint;
    /// a future sprint is refused by Jira (<see cref="SprintWriteProblem.NotActive"/>).
    /// </summary>
    public Task<WriteResult<WrittenSprint>> CompleteAsync(long sprintId, CancellationToken cancellationToken)
    {
        if (!IsValidId(sprintId))
        {
            return Refused(null, "A valid sprint id is required.");
        }

        return SendAsync(HttpMethod.Post, SprintPathFor(sprintId), SprintWriteBody.Complete(), cancellationToken);
    }

    private static string SprintPathFor(long sprintId) =>
        $"{SprintPath}/{sprintId.ToString(CultureInfo.InvariantCulture)}";

    private static string? StateName(SprintState state) => state switch
    {
        SprintState.Future => SprintWriteBody.FutureState,
        SprintState.Active => SprintWriteBody.ActiveState,
        SprintState.Closed => SprintWriteBody.ClosedState,
        _ => null,
    };

    private static bool IsValidId(long sprintId) => sprintId > 0;

    private static WriteResult<WrittenSprint>? CheckDates(DateTimeOffset? startDate, DateTimeOffset? endDate)
    {
        if (startDate is { } start && endDate is { } end && end <= start)
        {
            return Failure(SprintWriteFields.EndDate, SprintWriteErrors.EndBeforeStartMessage);
        }

        return null;
    }

    private static Task<WriteResult<WrittenSprint>> Refused(string? fieldId, string message) =>
        Task.FromResult(Failure(fieldId, message));

    private static WriteResult<WrittenSprint> Failure(string? fieldId, string message)
    {
        IReadOnlyList<WriteFieldError> fields = fieldId is null ? [] : [new WriteFieldError(fieldId, message)];

        var error = new WriteError(WriteErrorKind.Validation, null, message, fields, []);

        return WriteResults.Failure<WrittenSprint>(error);
    }

    private async Task<WriteResult<WrittenSprint>> SendAsync(
        HttpMethod method,
        string path,
        string body,
        CancellationToken cancellationToken)
    {
        var request = JiraRequest.Write(method, path, body);
        var result = await transport.SendWriteAsync(request, SprintWriteJsonContext.Default.Sprint, cancellationToken);

        if (!result.IsSuccess)
        {
            return WriteResults.Failure<WrittenSprint>(result.Error);
        }

        var sprint = SprintWriteMapper.Map(result.Value);

        if (sprint is null)
        {
            return WriteResults.Failure<WrittenSprint>(UnreadableReply());
        }

        return WriteResults.Success(sprint);
    }

    private static WriteError UnreadableReply() =>
        new(WriteErrorKind.UnknownOutcome, null, "Jira answered, but the reply is not a sprint.", [], []);
}
