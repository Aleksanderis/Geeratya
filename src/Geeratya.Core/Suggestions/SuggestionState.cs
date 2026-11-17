using Geeratya.Core.Lists;
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
/// <param name="Pick">The highlighted item and where the lookup is (<see cref="PickList"/>; -1 is no highlight).</param>
/// <param name="RequestId">The only lookup whose reply the popup accepts.</param>
public sealed record SuggestionState(
    int FieldIndex,
    SuggestionSource Source,
    string Query,
    IReadOnlyList<Suggestion> Pool,
    IReadOnlyList<Suggestion> Items,
    PickList Pick,
    long RequestId)
{
    /// <summary>A popup with the highlight on <paramref name="cursor"/>, in <paramref name="status"/>.</summary>
    public SuggestionState(
        int fieldIndex,
        SuggestionSource source,
        string query,
        IReadOnlyList<Suggestion> pool,
        IReadOnlyList<Suggestion> items,
        int cursor,
        PickStatus status,
        long requestId,
        string? error)
        : this(fieldIndex, source, query, pool, items, new PickList(cursor, status, error), requestId)
    {
    }

    /// <summary>The highlighted item index; -1 when nothing is highlighted.</summary>
    public int Cursor
    {
        get => Pick.Selected;
        init => Pick = Pick with { Selected = value };
    }

    /// <summary>Where the lookup is.</summary>
    public PickStatus Status
    {
        get => Pick.Status;
        init => Pick = Pick with { Status = value };
    }

    /// <summary>Why the last lookup failed, or null.</summary>
    public string? Error
    {
        get => Pick.Message;
        init => Pick = Pick with { Message = value };
    }

    /// <summary>The highlighted item, or null when nothing matches.</summary>
    public Suggestion? Selected => Cursor >= 0 && Cursor < Items.Count ? Items[Cursor] : null;
}
