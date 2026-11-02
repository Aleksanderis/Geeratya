using Geeratya.Core.History;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Filters.Models;

namespace Geeratya.Core.Sources;

/// <summary>
/// The boards and saved filters of the active site, kept for the session: they are loaded when the picker first
/// needs them and dropped with the rest of <c>SiteSession</c> on a profile change. A failed load is not kept, so the
/// next picker retries it.
/// </summary>
/// <param name="Boards">The boards, null until loaded.</param>
/// <param name="Filters">The saved filters (favourites first), null until loaded.</param>
/// <param name="BoardsLoading">A boards request is in flight.</param>
/// <param name="FiltersLoading">A filters request is in flight.</param>
/// <param name="BoardsError">Why the last boards load failed, or null.</param>
/// <param name="FiltersError">Why the last filters load failed, or null.</param>
public sealed record SourceLists(
    IReadOnlyList<BoardSummary>? Boards,
    IReadOnlyList<SavedFilter>? Filters,
    bool BoardsLoading,
    bool FiltersLoading,
    string? BoardsError,
    string? FiltersError)
{
    /// <summary>The recent JQL strings and issue keys of the profile, null until read from disk.</summary>
    public SiteHistory? History { get; init; }

    public static SourceLists Empty { get; } = new(null, null, false, false, null, null);

    public bool IsLoaded(SourceListKind kind)
    {
        return kind == SourceListKind.Boards ? Boards is not null : Filters is not null;
    }

    public bool IsLoading(SourceListKind kind)
    {
        return kind == SourceListKind.Boards ? BoardsLoading : FiltersLoading;
    }

    public string? ErrorOf(SourceListKind kind)
    {
        return kind == SourceListKind.Boards ? BoardsError : FiltersError;
    }

    /// <summary>The list is requested: loading, no error.</summary>
    public SourceLists Requested(SourceListKind kind)
    {
        return kind == SourceListKind.Boards
            ? this with { BoardsLoading = true, BoardsError = null }
            : this with { FiltersLoading = true, FiltersError = null };
    }

    public SourceLists Failed(SourceListKind kind, string message)
    {
        return kind == SourceListKind.Boards
            ? this with { BoardsLoading = false, BoardsError = message }
            : this with { FiltersLoading = false, FiltersError = message };
    }
}
