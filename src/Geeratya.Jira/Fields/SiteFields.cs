using System.Collections.Concurrent;
using Geeratya.Jira.Search;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Fields;

/// <summary>
/// The Sprint and estimate field ids of one connection, read once and kept for the life of the connection (a
/// profile switch builds a new instance). A successful answer is kept for good. A failure is kept for
/// <see cref="FailureMemory"/> so that callers that can do without the ids (issue detail) do not pay a failing
/// request on every open; after that the next call asks again. Callers that start together, or while a warm-up
/// from <see cref="Warm"/> is running, share one request. A board's estimation field, read from its configuration,
/// is remembered per board.
/// </summary>
public sealed class SiteFields(FieldClient client, TimeProvider? clock = null)
{
    /// <summary>How long a failed lookup is answered again without a request.</summary>
    public static readonly TimeSpan FailureMemory = TimeSpan.FromSeconds(30);

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly ConcurrentDictionary<long, string> _boardEstimates = new();
    private readonly object _lock = new();
    private volatile IssueExtraFields? _known;
    private Task<JiraResult<IssueExtraFields>>? _inflight;
    private JiraResult<IssueExtraFields>? _failure;
    private DateTimeOffset _failedAt;
    private int _warmed;

    /// <summary>
    /// Starts the lookup in the background (lowest priority) so the first issue detail finds the ids ready. Only the
    /// first call per connection does anything.
    /// </summary>
    public void Warm()
    {
        if (Interlocked.Exchange(ref _warmed, 1) == 1)
        {
            return;
        }

        _ = AskAsync(JiraPriority.Background);
    }

    /// <summary>The site-wide ids; one request the first time, none afterwards.</summary>
    public async Task<JiraResult<IssueExtraFields>> GetAsync(
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        if (_known is { } known)
        {
            return JiraResults.Success(known);
        }

        return await AskAsync(priority).WaitAsync(cancellationToken);
    }

    private Task<JiraResult<IssueExtraFields>> AskAsync(JiraPriority priority)
    {
        lock (_lock)
        {
            if (_known is { } known)
            {
                return Task.FromResult(JiraResults.Success(known));
            }

            if (_inflight is not null)
            {
                return _inflight;
            }

            if (_failure is { } failure && _clock.GetUtcNow() - _failedAt < FailureMemory)
            {
                return Task.FromResult(failure);
            }

            // The request belongs to the connection, not to the first caller: one caller leaving does not cancel it.
            var fetch = FetchAsync(priority);

            // A reply that completed synchronously has already stored its answer; keeping it here would stick.
            if (!fetch.IsCompleted)
            {
                _inflight = fetch;
            }

            return fetch;
        }
    }

    private async Task<JiraResult<IssueExtraFields>> FetchAsync(JiraPriority priority)
    {
        JiraResult<IssueExtraFields>? result = null;

        try
        {
            result = await client.GetExtraFieldsAsync(CancellationToken.None, priority);

            return result;
        }
        finally
        {
            // One step: a caller arriving now sees the answer, never an empty gap that would start a second request.
            lock (_lock)
            {
                _inflight = null;

                if (result is { IsSuccess: true })
                {
                    _known = result.Value;
                    _failure = null;
                }
                else if (result is not null)
                {
                    _failure = result;
                    _failedAt = _clock.GetUtcNow();
                }
            }
        }
    }

    /// <summary>Keeps the estimation field of a board's configuration; a null or blank id is ignored.</summary>
    public void RememberEstimate(long boardId, string? fieldId)
    {
        if (!string.IsNullOrWhiteSpace(fieldId))
        {
            _boardEstimates[boardId] = fieldId;
        }
    }

    /// <summary>
    /// Every estimate field id this connection knows: the ones remembered from board configurations first, then the
    /// site's candidates by name. The edit metadata of an issue decides which of them it can actually edit.
    /// </summary>
    public async Task<JiraResult<IReadOnlyList<string>>> GetEstimateCandidatesAsync(
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        var site = await GetAsync(cancellationToken, priority);

        if (!site.IsSuccess)
        {
            return JiraResults.Failure<IReadOnlyList<string>>(site.Error);
        }

        IReadOnlyList<string> ids =
        [
            .. _boardEstimates.OrderBy(entry => entry.Key).Select(entry => entry.Value).Concat(site.Value.EstimateFieldIds).Distinct(StringComparer.Ordinal),
        ];

        return JiraResults.Success(ids);
    }

    /// <summary>
    /// The extra fields for rows of <paramref name="boardId"/>: the site's Sprint field, the board's own estimation
    /// field first, then the site's candidates.
    /// </summary>
    public async Task<JiraResult<IssueExtraFields>> GetForBoardAsync(
        long boardId,
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        var site = await GetAsync(cancellationToken, priority);

        if (!site.IsSuccess || !_boardEstimates.TryGetValue(boardId, out var own))
        {
            return site;
        }

        return JiraResults.Success(site.Value with { EstimateFieldIds = [own, .. site.Value.EstimateFieldIds] });
    }
}
