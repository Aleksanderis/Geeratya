using Geeratya.Core.Suggestions;
using Geeratya.Jira.Suggestions.Models;

namespace Geeratya.Core.Forms;

/// <summary>
/// The suggestion popup of a select or multiple-choice field: the field's allowed values are the pool, ranked
/// locally by the typed filter (no Jira call, request 0). An empty filter lists every value with nothing
/// highlighted (so Enter moves on), except a required single choice without a value, which starts on its first
/// value; a single choice that is not required offers "(none)" first. Pure.
/// </summary>
public static class ChoicePopup
{
    private const string NoneLabel = "(none)";
    private const int ListLimit = 200;

    private static readonly Suggestion _none = new(string.Empty, NoneLabel, null, false);
    private static readonly SuggestionSource _source = new(SuggestionKind.Options, null, []);

    /// <summary>
    /// The popup for the choice field at <paramref name="index"/>. When <paramref name="previous"/> shows the same
    /// rows for the same field and filter it is returned as it is, so the cursor the user moved stays.
    /// </summary>
    public static SuggestionState For(FormField field, int index, SuggestionState? previous)
    {
        var query = field.Entry.Trim();
        var pool = field.Options.Select(option => new Suggestion(option.Id, option.Label, null, false)).ToList();
        var excluded = field.Kind == FormFieldKind.MultiSelect ? field.Value.Chosen : [];
        var items = Items(field, pool, query, excluded);

        if (previous is { Source.Kind: SuggestionKind.Options } same && same.FieldIndex == index
            && same.Query == query && same.Items.Select(item => item.Id).SequenceEqual(items.Select(item => item.Id)))
        {
            return same;
        }

        var cursor = query.Length > 0 || IsUnansweredRequired(field) ? 0 : -1;

        return new SuggestionState(index, _source, query, pool, items, cursor, SuggestionStatus.Loaded, 0, null);
    }

    // A required single choice without a value starts on its first value, so Enter picks it; every other field starts
    // with nothing highlighted, so Enter moves on.
    private static bool IsUnansweredRequired(FormField field)
    {
        return field is { Kind: FormFieldKind.Select, Required: true, Value.Chosen.Count: 0 };
    }

    private static List<Suggestion> Items(
        FormField field,
        List<Suggestion> pool,
        string query,
        IReadOnlyCollection<string> excluded)
    {
        if (query.Length > 0)
        {
            return [.. SuggestionRanker.Rank(pool, query, excluded, false)];
        }

        var all = pool.Where(candidate => !excluded.Contains(candidate.Id)).Take(ListLimit);
        var offersNone = field is { Kind: FormFieldKind.Select, Required: false };

        return offersNone ? [_none, .. all] : [.. all];
    }
}
