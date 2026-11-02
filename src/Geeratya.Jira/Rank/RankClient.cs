using Geeratya.Jira.Issues;
using Geeratya.Jira.Rank.Models;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Rank;

/// <summary>
/// Ranks issues relative to another issue and moves issues to the backlog (agile API). Both are writes: call them
/// from a command, never retry silently, and drop the cache of the views that show the issues after a success and
/// after an unknown outcome (the client keeps no cache). A rank write changes the values of other issues too and
/// the server order is the truth: reload the view instead of sorting locally. The reference issue must come from
/// the same view (Jira accepts one of another project without complaint). A 207 reply is a success of the call
/// with a per-issue <see cref="RankOutcome"/>: check <see cref="RankOutcome.AllRanked"/>.
/// </summary>
public sealed class RankClient(JiraTransport transport)
{
    private const string RankPath = "/rest/agile/1.0/issue/rank";
    private const string BacklogPath = "/rest/agile/1.0/backlog/issue";
    private const string BeforeName = "rankBeforeIssue";
    private const string AfterName = "rankAfterIssue";
    private const string FieldPrefix = "customfield_";
    private const int MaxIssues = 50;

    /// <summary>Puts <paramref name="keys"/> directly before <paramref name="referenceKey"/>.</summary>
    /// <remarks>
    /// <paramref name="rankFieldId"/> is the board's rank field (for example <c>customfield_10019</c>), or null to
    /// let Jira pick.
    /// </remarks>
    public Task<WriteResult<RankOutcome>> RankBeforeAsync(
        IReadOnlyList<string> keys,
        string referenceKey,
        string? rankFieldId,
        CancellationToken cancellationToken) =>
        RankAsync(keys, BeforeName, referenceKey, rankFieldId, cancellationToken);

    /// <summary>Puts <paramref name="keys"/> directly after <paramref name="referenceKey"/>.</summary>
    public Task<WriteResult<RankOutcome>> RankAfterAsync(
        IReadOnlyList<string> keys,
        string referenceKey,
        string? rankFieldId,
        CancellationToken cancellationToken) =>
        RankAsync(keys, AfterName, referenceKey, rankFieldId, cancellationToken);

    /// <summary>
    /// Takes <paramref name="keys"/> out of their sprint into the backlog (a Scrum operation; it did not change the
    /// status on the dev site's Kanban and team-managed boards, where "send to backlog" is a transition).
    /// </summary>
    public Task<WriteResult<RankOutcome>> MoveToBacklogAsync(
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken)
    {
        if (Validate(keys) is { } refused)
        {
            return Task.FromResult(refused);
        }

        return SendAsync(keys, HttpMethod.Post, BacklogPath, RankRequestBody.Backlog(keys), cancellationToken);
    }

    private Task<WriteResult<RankOutcome>> RankAsync(
        IReadOnlyList<string> keys,
        string referenceName,
        string referenceKey,
        string? rankFieldId,
        CancellationToken cancellationToken)
    {
        if (Validate(keys) is { } refused)
        {
            return Task.FromResult(refused);
        }

        if (!IssueUrls.IsValidKey(referenceKey))
        {
            return Task.FromResult(Refused("A valid reference issue key is required."));
        }

        if (keys.Contains(referenceKey, StringComparer.Ordinal))
        {
            return Task.FromResult(Refused("An issue cannot be ranked relative to itself."));
        }

        long? fieldId = null;

        if (rankFieldId is not null)
        {
            if (!rankFieldId.StartsWith(FieldPrefix, StringComparison.Ordinal)
                || !long.TryParse(rankFieldId.AsSpan(FieldPrefix.Length), out var parsed)
                || parsed <= 0)
            {
                return Task.FromResult(Refused("The rank field id is not valid."));
            }

            fieldId = parsed;
        }

        var body = RankRequestBody.Rank(keys, referenceName, referenceKey, fieldId);

        return SendAsync(keys, HttpMethod.Put, RankPath, body, cancellationToken);
    }

    private async Task<WriteResult<RankOutcome>> SendAsync(
        IReadOnlyList<string> keys,
        HttpMethod method,
        string path,
        string body,
        CancellationToken cancellationToken)
    {
        var request = JiraRequest.Write(method, path, body) with { AllowsEmptyBody = true };
        var result = await transport.SendWriteAsync(request, RankJsonContext.Default.Response, cancellationToken);

        return result.IsSuccess
            ? WriteResults.Success(RankMapper.Map(keys, result.Value))
            : WriteResults.Failure<RankOutcome>(result.Error);
    }

    private static WriteResult<RankOutcome>? Validate(IReadOnlyList<string> keys)
    {
        if (keys.Count == 0)
        {
            return Refused("At least one issue is required.");
        }

        if (keys.Count > MaxIssues)
        {
            return Refused($"At most {MaxIssues} issues can be moved in one call.");
        }

        if (keys.Any(key => !IssueUrls.IsValidKey(key)) || keys.Distinct(StringComparer.Ordinal).Count() != keys.Count)
        {
            return Refused("Every issue key must be valid and listed once.");
        }

        return null;
    }

    private static WriteResult<RankOutcome> Refused(string message) =>
        WriteResults.Failure<RankOutcome>(new WriteError(WriteErrorKind.Validation, null, message, [], []));
}
