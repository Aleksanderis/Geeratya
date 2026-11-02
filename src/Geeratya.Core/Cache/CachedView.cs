using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Cache;

/// <summary>The first page of a view as last seen, kept so the view opens before the network answers.</summary>
/// <param name="Rows">The first page of rows, at most <c>ViewPaging.PageSize</c>.</param>
/// <param name="Board">The board configuration for board sources, otherwise null.</param>
/// <param name="SavedAt">When the rows came from the network.</param>
public sealed record CachedView(IReadOnlyList<IssueSummary> Rows, BoardConfiguration? Board, DateTimeOffset SavedAt);
