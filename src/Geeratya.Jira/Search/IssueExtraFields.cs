namespace Geeratya.Jira.Search;

/// <summary>
/// The site-specific field ids a request adds to the fixed list-row fields: the Sprint field and the estimate field(s).
/// Ids differ per site, so they are resolved per connection and passed in, never written in code.
/// </summary>
/// <param name="SprintFieldId">The Sprint field id; null when the site has none or it was not resolved.</param>
/// <param name="EstimateFieldIds">Candidate estimate field ids, the preferred one first; empty for none.</param>
public sealed record IssueExtraFields(string? SprintFieldId, IReadOnlyList<string> EstimateFieldIds)
{
    /// <summary>No extra fields: what every request asked for before sprints were read.</summary>
    public static IssueExtraFields None { get; } = new(null, []);

    /// <summary>Also read the <c>subtasks</c> field (the columns layout shows a sub-task row on a card).</summary>
    public bool IncludeSubtasks { get; init; }

    /// <summary>The extra ids to append to a field list, without repeats.</summary>
    public IEnumerable<string> Ids()
    {
        if (IncludeSubtasks)
        {
            yield return IssueFields.Subtasks;
        }

        if (SprintFieldId is { Length: > 0 } sprint)
        {
            yield return sprint;
        }

        foreach (var id in EstimateFieldIds.Distinct(StringComparer.Ordinal))
        {
            if (id.Length > 0 && id != SprintFieldId)
            {
                yield return id;
            }
        }
    }

    /// <summary>The comma-separated field list of <paramref name="baseFields"/> plus these ids.</summary>
    public string QueryValue(IReadOnlyList<string> baseFields) =>
        string.Join(',', baseFields.Concat(Ids().Select(Uri.EscapeDataString)));

    /// <summary>The field list as the array a POST body carries.</summary>
    public IReadOnlyList<string> FieldList(IReadOnlyList<string> baseFields) => [.. baseFields, .. Ids()];
}
