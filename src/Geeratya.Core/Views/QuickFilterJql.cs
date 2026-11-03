namespace Geeratya.Core.Views;

/// <summary>The JQL the switched-on quick filters of a tab add to its board source (#192). Pure.</summary>
public static class QuickFilterJql
{
    /// <summary>
    /// The clauses of the active filters joined with <c>AND</c>, each in parentheses so an <c>OR</c> inside one stays
    /// inside it; null when none is on or the filters are not known.
    /// </summary>
    public static string? For(TabState tab)
    {
        if (tab.ActiveQuickFilters.Count == 0)
        {
            return null;
        }

        var clauses = tab.QuickFilters
            .Where(filter => tab.ActiveQuickFilters.Contains(filter.Id))
            .Select(filter => $"({filter.Jql})")
            .ToList();

        return clauses.Count == 0 ? null : string.Join(" AND ", clauses);
    }

    /// <summary>
    /// <paramref name="source"/> narrowed by <paramref name="extra"/>: the source in parentheses (a backlog query holds
    /// an <c>OR</c>) then the filters; whichever is missing is left out.
    /// </summary>
    public static string? Combine(string? source, string? extra)
    {
        if (string.IsNullOrWhiteSpace(extra))
        {
            return source;
        }

        return string.IsNullOrWhiteSpace(source) ? extra : $"({source}) AND {extra}";
    }
}
