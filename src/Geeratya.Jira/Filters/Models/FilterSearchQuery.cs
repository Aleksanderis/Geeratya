using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Filters.Models;

/// <summary>Which filters to search.</summary>
/// <param name="Name">Only filters whose name contains this text (null or blank: all visible filters).</param>
/// <param name="PageSize">Filters per page, clamped to 1 to <see cref="FilterClient.MaxPageSize"/>.</param>
public sealed record FilterSearchQuery(string? Name = null, int PageSize = FilterSearchQuery.DefaultPageSize)
{
    public const int DefaultPageSize = 50;

    /// <summary>Who is waiting for the answer. Defaults to <see cref="JiraPriority.User"/>.</summary>
    public JiraPriority Priority { get; init; } = JiraPriority.User;
}
