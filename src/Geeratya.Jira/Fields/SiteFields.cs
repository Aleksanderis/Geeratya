using System.Collections.Concurrent;
using Geeratya.Jira.Search;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Fields;

/// <summary>
/// The Sprint and estimate field ids of one connection, read once and kept for the life of the connection (a
/// profile switch builds a new instance). Only a successful answer is kept; a failure is returned and the next call
/// asks again. A board's estimation field, read from its configuration, is remembered per board.
/// </summary>
public sealed class SiteFields(FieldClient client)
{
    private readonly ConcurrentDictionary<long, string> _boardEstimates = new();
    private volatile IssueExtraFields? _known;

    /// <summary>The site-wide ids; one request the first time, none afterwards.</summary>
    public async Task<JiraResult<IssueExtraFields>> GetAsync(
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        if (_known is { } known)
        {
            return JiraResults.Success(known);
        }

        // Two callers that start together may both ask; the answer is the same and the request is a cheap GET.
        var result = await client.GetExtraFieldsAsync(cancellationToken, priority);

        if (result.IsSuccess)
        {
            _known = result.Value;
        }

        return result;
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
            .. _boardEstimates.Values.Concat(site.Value.EstimateFieldIds).Distinct(StringComparer.Ordinal),
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
