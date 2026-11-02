using Geeratya.Jira.Suggestions.Models;

namespace Geeratya.Core.Suggestions;

/// <summary>
/// An open suggestion popup of the focused form field. Plain data: the query is the field's typing buffer, the pool
/// is every candidate loaded so far (matched locally at once while a lookup runs), the items are what is shown.
/// </summary>
/// <param name="FieldIndex">The form field the popup belongs to.</param>
/// <param name="Source">What is searched.</param>
/// <param name="Query">The text being matched.</param>
/// <param name="Pool">Candidates loaded so far.</param>
/// <param name="Items">The ranked candidates shown, best first.</param>
/// <param name="Cursor">The highlighted item.</param>
/// <param name="Status">Where the lookup is.</param>
/// <param name="RequestId">The only lookup whose reply the popup accepts.</param>
/// <param name="Error">Why the last lookup failed, or null.</param>
public sealed record SuggestionState(
    int FieldIndex,
    SuggestionSource Source,
    string Query,
    IReadOnlyList<Suggestion> Pool,
    IReadOnlyList<Suggestion> Items,
    int Cursor,
    SuggestionStatus Status,
    long RequestId,
    string? Error)
{
    /// <summary>The highlighted item, or null when nothing matches.</summary>
    public Suggestion? Selected => Cursor >= 0 && Cursor < Items.Count ? Items[Cursor] : null;
}
