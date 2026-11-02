namespace Geeratya.Core.History;

/// <summary>
/// The recent JQL strings and issue keys of one profile, newest first, capped and without duplicates. Pure: every
/// change returns a new value. Only what the user typed or opened is kept, never a token or other secret.
/// </summary>
/// <param name="Queries">The JQL strings, newest first.</param>
/// <param name="Issues">The issue keys (upper case), newest first.</param>
public sealed record SiteHistory(IReadOnlyList<string> Queries, IReadOnlyList<string> Issues)
{
    /// <summary>The most JQL strings kept.</summary>
    public const int MaxQueries = 50;

    /// <summary>The most issue keys kept.</summary>
    public const int MaxIssues = 50;

    /// <summary>A longer JQL string is not remembered.</summary>
    public const int MaxQueryLength = 2000;

    private const int MaxKeyLength = 64;

    public static SiteHistory Empty { get; } = new([], []);

    /// <summary>Puts <paramref name="query"/> first; an equal one is moved, the oldest falls off at the cap.</summary>
    public SiteHistory WithQuery(string query)
    {
        var text = query.Trim();

        if (text.Length == 0 || text.Length > MaxQueryLength)
        {
            return this;
        }

        return this with { Queries = Pushed(Queries, text, MaxQueries) };
    }

    /// <summary>Puts the issue <paramref name="key"/> first (upper-cased); an equal one is moved.</summary>
    public SiteHistory WithIssue(string key)
    {
        var text = key.Trim().ToUpperInvariant();

        if (text.Length == 0 || text.Length > MaxKeyLength)
        {
            return this;
        }

        return this with { Issues = Pushed(Issues, text, MaxIssues) };
    }

    /// <summary>A copy that obeys the rules above, for data read from a file someone may have edited.</summary>
    public SiteHistory Normalized()
    {
        var result = Empty;

        foreach (var query in (Queries ?? []).Reverse())
        {
            result = result.WithQuery(query ?? string.Empty);
        }

        foreach (var key in (Issues ?? []).Reverse())
        {
            result = result.WithIssue(key ?? string.Empty);
        }

        return result;
    }

    private static string[] Pushed(IReadOnlyList<string> list, string value, int cap)
    {
        var others = list.Where(item => !string.Equals(item, value, StringComparison.Ordinal));

        return [.. new[] { value }.Concat(others).Take(cap)];
    }
}
