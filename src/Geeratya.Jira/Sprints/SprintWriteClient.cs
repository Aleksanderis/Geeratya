using System.Globalization;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Sprints;

/// <summary>
/// Creates, edits, starts and completes sprints (agile API). Every method is a write: call it only from a confirmed
/// command, never retry silently, and reload the sprint after an unknown outcome (<see cref="WriteError.IsOutcomeUnknown"/>).
/// The client keeps no cache. Updates, start and complete use <c>POST /sprint/{id}</c>, the partial update: <c>PUT</c>
/// replaces the sprint and drops an omitted goal. <see cref="StartAsync"/> and <see cref="CompleteAsync"/> change the
/// workflow of a team (the UI confirms them first, #186). Jira allows several active sprints at once, so there is no
/// "another sprint is active" error. Completing does not move unfinished issues: <c>moveIssuesTo</c> is rejected with
/// 400, so they stay in the closed sprint and show up in the backlog; moving them is a second call (open question 8
/// in docs/roadmap.md). Only call these on a Scrum board's sprints (a Kanban board has none).
/// </summary>
public sealed class SprintWriteClient(JiraTransport transport)
{
    private const string SprintPath = "/rest/agile/1.0/sprint";
    private const string EndDateField = "endDate";
    private const string NameField = "name";

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
            return Refused(NameField, "A sprint needs a name.");
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

        return SendAsync(SprintPath, body, cancellationToken);
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

        if (edit.Name is not null && string.IsNullOrWhiteSpace(edit.Name))
        {
            return Refused(NameField, "A sprint needs a name.");
        }

        if (CheckDates(edit.StartDate, edit.EndDate) is { } refused)
        {
            return Task.FromResult(refused);
        }

        var body = SprintWriteBody.Update(edit.Name?.Trim(), edit.Goal, edit.StartDate, edit.EndDate);

        return SendAsync(SprintPathFor(sprintId), body, cancellationToken);
    }

    /// <summary>
    /// Starts a future sprint with the given dates (<c>future</c> to <c>active</c>). Starting an active sprint is a
    /// no-op in Jira; starting a closed one is refused (<see cref="SprintWriteProblem.AlreadyClosed"/>).
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

        return SendAsync(SprintPathFor(sprintId), SprintWriteBody.Start(startDate, endDate), cancellationToken);
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

        return SendAsync(SprintPathFor(sprintId), SprintWriteBody.Complete(), cancellationToken);
    }

    private static string SprintPathFor(long sprintId) =>
        $"{SprintPath}/{sprintId.ToString(CultureInfo.InvariantCulture)}";

    private static bool IsValidId(long sprintId) => sprintId > 0;

    private static WriteResult<WrittenSprint>? CheckDates(DateTimeOffset? startDate, DateTimeOffset? endDate)
    {
        if (startDate is { } start && endDate is { } end && end <= start)
        {
            return Failure(EndDateField, SprintWriteErrors.EndBeforeStartMessage);
        }

        return null;
    }

    private static Task<WriteResult<WrittenSprint>> Refused(string? fieldId, string message) =>
        Task.FromResult(Failure(fieldId, message));

    private static WriteResult<WrittenSprint> Failure(string? fieldId, string message)
    {
        IReadOnlyList<WriteFieldError> fields = fieldId is null ? [] : [new WriteFieldError(fieldId, message)];

        return WriteResults.Failure<WrittenSprint>(new WriteError(WriteErrorKind.Validation, null, message, fields, []));
    }

    private async Task<WriteResult<WrittenSprint>> SendAsync(
        string path,
        string body,
        CancellationToken cancellationToken)
    {
        var request = JiraRequest.Write(HttpMethod.Post, path, body);
        var result = await transport.SendWriteAsync(request, SprintWriteJsonContext.Default.Sprint, cancellationToken);

        if (!result.IsSuccess)
        {
            return WriteResults.Failure<WrittenSprint>(result.Error);
        }

        var sprint = SprintWriteMapper.Map(result.Value);

        return sprint is null
            ? WriteResults.Failure<WrittenSprint>(UnreadableReply())
            : WriteResults.Success(sprint);
    }

    private static WriteError UnreadableReply() =>
        new(WriteErrorKind.UnknownOutcome, null, "Jira answered, but the reply is not a sprint.", [], []);
}
