using Geeratya.Core.Links;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Writes;
using Geeratya.Core.Writes.Comments;
using Geeratya.Jira.Jql;
using Geeratya.Jira.Search;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.App;

public sealed partial class WriteHandler
{
    /// <summary>
    /// A lost reply of a create: the recent issues of the account in the project are searched for the same type and
    /// summary. Found means it was created; not found proves nothing (the index lags), so the outcome stays unknown.
    /// </summary>
    private async Task<Command?> VerifyCreateAsync(
        JiraServices services,
        VerifyWriteEffect effect,
        CreateRequest create,
        CancellationToken token)
    {
        try
        {
            // Only issues created since the submit count (minutes rounded up, relative to the server's now,
            // so no time zone is involved); without a remembered submit time the old ten minutes apply.
            var recent = _createSubmitted.TryGetValue(effect.OperationId, out var submitted)
                ? JqlBuilder.OwnIssuesSince((int)Math.Ceiling((_clock.GetUtcNow() - submitted).TotalMinutes))
                : JqlBuilder.RecentOwnIssues();
            var jql = JqlBuilder.WithOrder(
                JqlBuilder.And(JqlBuilder.Project(create.ProjectKey), recent),
                JqlOrder.CreatedDescending);
            var result = await services.Search.SearchAsync(new IssueSearchQuery(jql), null, token);
            var found = result.IsSuccess ? result.Value.Items.Where(create.IsShownBy).Take(1).ToArray() : [];
            var outcome = found.Length > 0 ? VerifyOutcome.Applied : VerifyOutcome.Unreachable;

            if (outcome == VerifyOutcome.Applied)
            {
                _createSubmitted.TryRemove(effect.OperationId, out _);
            }

            return new WriteVerified(effect.OperationId, _clock.GetUtcNow(), outcome, found);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A read that breaks is a read that failed: it never changes what is known about the write.
            return new WriteVerified(effect.OperationId, _clock.GetUtcNow(), VerifyOutcome.Unreachable, []);
        }
    }

    /// <summary>
    /// The write was accepted: reads the issues again (one search) for the server rows. The read is best effort: a
    /// failed one, or one that still shows the old value, leaves the rows to the next reload.
    /// </summary>
    private async Task<Command> SucceededAsync(
        JiraServices services,
        ExecuteWriteEffect effect,
        CancellationToken token)
    {
        if (effect.Request.RemovesIssues || !effect.Request.ReadsBack)
        {
            return new WriteSucceeded(effect.OperationId, _clock.GetUtcNow(), []);
        }

        var rows = await ReadKeysAsync(services, effect.Request.Keys, effect.Request.ReadBackFieldIds, token);
        var shown = rows is null ? [] : rows.Where(effect.Request.IsShownBy).ToArray();

        return new WriteSucceeded(effect.OperationId, _clock.GetUtcNow(), shown);
    }

    private async Task<Command?> VerifyAsync(
        JiraServices services,
        VerifyWriteEffect effect,
        CancellationToken token)
    {
        if (effect.Request is CommentRequest comment)
        {
            var check = await CommentWriter.VerifyAsync(services, comment, token);

            return new WriteVerified(effect.OperationId, _clock.GetUtcNow(), check.Outcome, [], check.Comment);
        }

        if (effect.Request is DeleteIssueRequest delete)
        {
            return await VerifyDeleteAsync(services, effect, delete, token);
        }

        if (effect.Request is CreateRequest create)
        {
            return await VerifyCreateAsync(services, effect, create, token);
        }

        if (effect.Request is SprintWriteRequest sprintRequest)
        {
            return await VerifySprintAsync(services, effect, sprintRequest, token);
        }

        if (effect.Request is LinkWriteRequest linkRequest)
        {
            return await VerifyLinkAsync(services, effect, linkRequest, token);
        }

        if (!effect.Request.ReadsBack)
        {
            // Nothing a read of the issue shows can tell (a rank): the user is told to reload.
            return new WriteVerified(effect.OperationId, _clock.GetUtcNow(), VerifyOutcome.Unreachable, []);
        }

        var rows = await ReadKeysAsync(services, effect.Request.Keys, effect.Request.ReadBackFieldIds, token);
        var outcome = OutcomeOf(effect.Request, rows);

        return new WriteVerified(effect.OperationId, _clock.GetUtcNow(), outcome, rows ?? []);
    }

    /// <summary>
    /// A lost reply of a delete: the issue is read by key (a read that does not lag like the search index). Gone
    /// means the delete happened, readable means it did not, anything else is unknown.
    /// </summary>
    private async Task<Command?> VerifyDeleteAsync(
        JiraServices services,
        VerifyWriteEffect effect,
        DeleteIssueRequest delete,
        CancellationToken token)
    {
        var outcome = VerifyOutcome.Unreachable;

        try
        {
            var result = await services.Issues.GetIssueAsync(delete.Key, token);

            if (result.IsSuccess)
            {
                outcome = VerifyOutcome.NotApplied;
            }
            else if (result.Error.Kind == JiraErrorKind.NotFound)
            {
                outcome = VerifyOutcome.Applied;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A read that breaks is a read that failed: it never changes what is known about the write.
        }

        return new WriteVerified(effect.OperationId, _clock.GetUtcNow(), outcome, []);
    }

    /// <summary>
    /// A lost reply of a link write: the issue is read by key (links are part of it, and the read does not lag like
    /// the search index). The issue shows the change or it does not; a read that fails decides nothing.
    /// </summary>
    private async Task<Command?> VerifyLinkAsync(
        JiraServices services,
        VerifyWriteEffect effect,
        LinkWriteRequest request,
        CancellationToken token)
    {
        var outcome = VerifyOutcome.Unreachable;

        try
        {
            var result = await services.Issues.GetIssueAsync(request.Key, token);

            if (result.IsSuccess)
            {
                outcome = request.IsShownBy(result.Value) ? VerifyOutcome.Applied : VerifyOutcome.NotApplied;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A read that breaks is a read that failed: it never changes what is known about the write.
        }

        return new WriteVerified(effect.OperationId, _clock.GetUtcNow(), outcome, []);
    }

    private static VerifyOutcome OutcomeOf(WriteRequest request, IReadOnlyList<IssueSummary>? rows)
    {
        if (rows is null || rows.Count < request.Keys.Count)
        {
            return VerifyOutcome.Unreachable;
        }

        // The read is a search, and the search index lags behind a write: an old value proves nothing, so only a
        // row that shows the change decides. "Not applied" needs a read that cannot lag (none is used yet).
        return rows.All(request.IsShownBy) ? VerifyOutcome.Applied : VerifyOutcome.Unreachable;
    }

    private static async Task<IReadOnlyList<IssueSummary>?> ReadKeysAsync(
        JiraServices services,
        IReadOnlyList<string> keys,
        IReadOnlyList<string> extraFieldIds,
        CancellationToken token)
    {
        try
        {
            var query = new IssueSearchQuery(JqlBuilder.KeyIn(keys), keys.Count)
            {
                Extras = new IssueExtraFields(null, extraFieldIds),
            };
            var result = await services.Search.SearchAsync(query, null, token);

            return result.IsSuccess ? result.Value.Items : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A read that breaks is a read that failed: it never changes what is known about the write.
            return null;
        }
    }

    private static WriteError NotSent()
    {
        return new WriteError(WriteErrorKind.NotSent, null, NoConnectionMessage, [], []);
    }
}
