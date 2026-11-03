using System.Globalization;
using Geeratya.Jira.Issues;
using Geeratya.Jira.Rank;
using Geeratya.Jira.Rank.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Sprints;

/// <summary>
/// Moves issues into a sprint (<c>POST /sprint/{id}/issue</c>) or out of their sprint into the backlog
/// (<c>POST /backlog/issue</c>), agile API. Both are writes: call them from a confirmed command (Jira has no
/// undo; a move can drop an issue from another open sprint, an issue is in one open sprint at a time). Jira takes
/// at most 50 keys per call, so longer lists are sent in chunks one after the other; the run is not atomic and the
/// <see cref="SprintMoveOutcome"/> says which keys moved and which did not. Moving into a closed sprint is a 400 and
/// moves nothing. The client keeps no cache: reload the views that show the issues afterwards, also after an
/// unknown outcome. Use it for Scrum boards only.
/// </summary>
public sealed class SprintMoveClient(JiraTransport transport)
{
    /// <summary>Jira's limit of keys per call.</summary>
    public const int ChunkSize = 50;

    private const string SprintPath = "/rest/agile/1.0/sprint";
    private const string BacklogPath = "/rest/agile/1.0/backlog/issue";

    public Task<WriteResult<SprintMoveOutcome>> MoveToSprintAsync(
        long sprintId,
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken)
    {
        if (sprintId <= 0)
        {
            return Task.FromResult(Refused("A valid sprint id is required."));
        }

        var path = $"{SprintPath}/{sprintId.ToString(CultureInfo.InvariantCulture)}/issue";

        return MoveAsync(path, keys, cancellationToken);
    }

    public Task<WriteResult<SprintMoveOutcome>> MoveToBacklogAsync(
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken) =>
        MoveAsync(BacklogPath, keys, cancellationToken);

    private async Task<WriteResult<SprintMoveOutcome>> MoveAsync(
        string path,
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken)
    {
        if (keys.Count == 0)
        {
            return Refused("At least one issue is required.");
        }

        if (keys.Any(key => !IssueUrls.IsValidKey(key)) || keys.Distinct(StringComparer.Ordinal).Count() != keys.Count)
        {
            return Refused("Every issue key must be valid and listed once.");
        }

        List<string> moved = [];
        List<RankItemResult> rejected = [];
        List<string> unknown = [];
        List<string> notAttempted = [];
        WriteError? stopped = null;

        foreach (var chunk in keys.Chunk(ChunkSize))
        {
            if (stopped is not null)
            {
                notAttempted.AddRange(chunk);
                continue;
            }

            var result = await SendAsync(path, chunk, cancellationToken);

            if (result.IsSuccess)
            {
                moved.AddRange(result.Value.Items.Where(item => item.IsRanked).Select(item => item.Key));
                rejected.AddRange(result.Value.Failed);
            }
            else if (result.Error.IsOutcomeUnknown)
            {
                unknown.AddRange(chunk);
                stopped = result.Error;
            }
            else
            {
                rejected.AddRange(chunk.Select(key => new RankItemResult(key, false, [result.Error.Message])));
                stopped = result.Error;
            }
        }

        return WriteResults.Success(new SprintMoveOutcome(moved, rejected, unknown, notAttempted, stopped));
    }

    private async Task<WriteResult<RankOutcome>> SendAsync(
        string path,
        string[] chunk,
        CancellationToken cancellationToken)
    {
        var body = RankRequestBody.Backlog(chunk);
        var request = JiraRequest.Write(HttpMethod.Post, path, body) with { AllowsEmptyBody = true };

        try
        {
            var result = await transport.SendWriteAsync(request, RankJsonContext.Default.Response, cancellationToken);

            return result.IsSuccess
                ? WriteResults.Success(RankMapper.Map(chunk, result.Value))
                : WriteResults.Failure<RankOutcome>(result.Error);
        }
        catch (OperationCanceledException)
        {
            // The chunk may have been applied: report it as unknown so the chunks that did move are not lost.
            var error = new WriteError(WriteErrorKind.UnknownOutcome, null, "The move was cancelled.", [], []);

            return WriteResults.Failure<RankOutcome>(error);
        }
    }

    private static WriteResult<SprintMoveOutcome> Refused(string message) =>
        WriteResults.Failure<SprintMoveOutcome>(new WriteError(WriteErrorKind.Validation, null, message, [], []));
}
