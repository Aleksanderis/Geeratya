using Geeratya.Jira.Suggestions;
using Geeratya.Jira.Suggestions.Models;

namespace Geeratya.Core.Suggestions;

/// <summary>
/// The transitions of a <see cref="SuggestionState"/>: a new query, a reply, a slow or failed lookup, the cursor.
/// Pure and clock-free: the debounce is a delay the effect carries, so the state never reads time. A reply is
/// accepted only for the request the popup waits for (the stale-reply rule of the write pipeline).
/// </summary>
public static class SuggestionPopup
{
    /// <summary>How long typing must pause before Jira is asked.</summary>
    public static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(250);

    /// <summary>How long a lookup may run before the popup says it is searching.</summary>
    public static readonly TimeSpan SlowAfter = TimeSpan.FromMilliseconds(150);

    private const int PoolLimit = 200;

    /// <summary>
    /// The popup after the query changed: matched at once against what is loaded, waiting for the new request.
    /// <paramref name="previous"/> of the same field keeps its pool.
    /// </summary>
    public static SuggestionState Queried(
        SuggestionState? previous,
        int fieldIndex,
        SuggestionSource source,
        string query,
        IReadOnlyCollection<string> excluded,
        long requestId)
    {
        var pool = previous is not null && previous.FieldIndex == fieldIndex
            ? previous.Pool
            : [.. source.KnownLabels.Select(SuggestionFilter.FromLabel)];

        var items = SuggestionRanker.Rank(pool, query, excluded, source.AllowsFreeText);

        return new SuggestionState(
            fieldIndex,
            source,
            query,
            pool,
            items,
            0,
            SuggestionStatus.Waiting,
            requestId,
            null);
    }

    /// <summary>The popup with the reply of its current request; null when the reply is not for it.</summary>
    public static SuggestionState? Loaded(
        SuggestionState state,
        long requestId,
        IReadOnlyList<Suggestion> reply,
        IReadOnlyCollection<string> excluded)
    {
        if (state.RequestId != requestId)
        {
            return null;
        }

        var pool = state.Pool.Concat(reply).DistinctBy(candidate => candidate.Id, StringComparer.Ordinal);
        var kept = pool.Take(PoolLimit).ToList();
        var items = state.Source.AllowsFreeText ? Ranked(state, kept, excluded) : Replied(reply, excluded);

        return state with { Pool = kept, Items = items, Cursor = 0, Status = SuggestionStatus.Loaded, Error = null };
    }

    /// <summary>The popup marked as searching; null when the notice is not for its current request or too late.</summary>
    public static SuggestionState? Slow(SuggestionState state, long requestId)
    {
        if (state.RequestId != requestId || state.Status != SuggestionStatus.Waiting)
        {
            return null;
        }

        return state with { Status = SuggestionStatus.Loading };
    }

    /// <summary>The popup after a failed lookup, still showing what is loaded; null when not for its request.</summary>
    public static SuggestionState? Failed(SuggestionState state, long requestId, string message)
    {
        if (state.RequestId != requestId)
        {
            return null;
        }

        return state with { Status = SuggestionStatus.Failed, Error = message };
    }

    /// <summary>The cursor moved by <paramref name="delta"/> rows, kept inside the list.</summary>
    public static SuggestionState Moved(SuggestionState state, int delta)
    {
        var last = Math.Max(0, state.Items.Count - 1);

        return state with { Cursor = Math.Clamp(state.Cursor + delta, 0, last) };
    }

    private static List<Suggestion> Ranked(
        SuggestionState state,
        IEnumerable<Suggestion> pool,
        IReadOnlyCollection<string> excluded)
    {
        return [.. SuggestionRanker.Rank(pool, state.Query, excluded, state.Source.AllowsFreeText)];
    }

    // Jira's own order stands for people and issues: it also matches on e-mail and key, which a label match misses.
    private static List<Suggestion> Replied(IEnumerable<Suggestion> reply, IReadOnlyCollection<string> excluded)
    {
        return [.. reply.Where(candidate => !excluded.Contains(candidate.Id)).Take(SuggestionFilter.MaxResults)];
    }
}
