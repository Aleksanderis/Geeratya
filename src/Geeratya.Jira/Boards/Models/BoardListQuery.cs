using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Boards.Models;

/// <summary>Which boards to list. Both filters are optional.</summary>
/// <param name="ProjectKeyOrId">Only boards of this project.</param>
/// <param name="Type">Only boards of this kind (null or <see cref="BoardType.Unknown"/>: all).</param>
/// <param name="PageSize">Boards per page, clamped to 1 to <see cref="BoardClient.MaxPageSize"/>.</param>
public sealed record BoardListQuery(
    string? ProjectKeyOrId = null,
    BoardType? Type = null,
    int PageSize = BoardListQuery.DefaultPageSize)
{
    public const int DefaultPageSize = 50;

    /// <summary>Who is waiting for the answer. Defaults to <see cref="JiraPriority.User"/>.</summary>
    public JiraPriority Priority { get; init; } = JiraPriority.User;
}
