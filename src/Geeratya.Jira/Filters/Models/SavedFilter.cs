namespace Geeratya.Jira.Filters.Models;

/// <summary>
/// A saved filter. <c>Jql</c> and <c>OwnerName</c> are null when Jira did not send them (no <c>expand</c>, or a
/// removed owner). Board filters ("Filter for ... board") are ordinary filters and are kept.
/// </summary>
/// <param name="Id">The filter id; running it is the JQL <c>filter = &lt;id&gt;</c>.</param>
/// <param name="Name">The filter name.</param>
/// <param name="Jql">The filter's JQL, when sent.</param>
/// <param name="OwnerName">The owner's display name, when sent.</param>
/// <param name="IsFavourite">Whether the caller starred it (false when Jira omits the field, as in search).</param>
/// <param name="ApproximateLastUsed">When the filter was last used, roughly, when sent.</param>
public sealed record SavedFilter(
    string Id,
    string Name,
    string? Jql,
    string? OwnerName,
    bool IsFavourite,
    DateTimeOffset? ApproximateLastUsed);
