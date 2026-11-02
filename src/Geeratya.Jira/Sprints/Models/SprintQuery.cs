using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Sprints.Models;

/// <summary>Which sprints of a board to list and how many per page.</summary>
/// <param name="States">Only sprints in these states; null or empty for all. Unknown is ignored.</param>
/// <param name="PageSize">Sprints per page, clamped to 1 to <see cref="SprintClient.MaxPageSize"/>.</param>
public sealed record SprintQuery(IReadOnlyList<SprintState>? States = null, int PageSize = SprintQuery.DefaultPageSize)
{
    public const int DefaultPageSize = 50;

    /// <summary>Who is waiting for the answer. Defaults to <see cref="JiraPriority.User"/>.</summary>
    public JiraPriority Priority { get; init; } = JiraPriority.User;
}
