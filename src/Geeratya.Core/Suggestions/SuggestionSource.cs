namespace Geeratya.Core.Suggestions;

/// <summary>What a popup searches: the kind plus what the lookup needs.</summary>
/// <param name="Kind">The lookup.</param>
/// <param name="IssueKey">The issue the field belongs to (users and the parent); null for labels and in a create form (see <paramref name="ProjectKey"/>).</param>
/// <param name="KnownLabels">Labels already visible in the view: matched locally, before Jira answers.</param>
/// <param name="ProjectKey">The project of a create form (no issue exists yet): users and parents are searched in it; null otherwise.</param>
/// <param name="ParentLevel">The hierarchy level a parent must have (one above the issue type, 0 for a sub-task, see <see cref="Geeratya.Core.Forms.ParentLevels"/>); null when unknown or not a parent lookup: nothing is narrowed.</param>
public sealed record SuggestionSource(
    SuggestionKind Kind,
    string? IssueKey,
    IReadOnlyList<string> KnownLabels,
    string? ProjectKey = null,
    int? ParentLevel = null)
{
    /// <summary>A text that matches nothing may still be written (a new label).</summary>
    public bool AllowsFreeText => Kind == SuggestionKind.Labels;
}
