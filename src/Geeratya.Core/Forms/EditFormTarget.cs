using Geeratya.Core.Writes;

namespace Geeratya.Core.Forms;

/// <summary>
/// Edit fields of one issue. <paramref name="Known"/> holds the current values the opener already has (from the row
/// or the detail), by field id: the editor metadata carries no values, and a field with an unknown current value would
/// be overwritten blind, so only fields with a known value are offered when <paramref name="OnlyKnown"/> is set.
/// <paramref name="Only"/> limits the form to one kind of field (the labels pilot).
/// </summary>
public sealed record EditFormTarget(
    string Key,
    IReadOnlyDictionary<string, FormValue> Known,
    bool OnlyKnown,
    FormFieldKind? Only) : FormTarget
{
    public override string Title => $"Edit {Key}";

    public override string? IssueKey => Key;

    public override WriteRequest Request(IReadOnlyList<FieldChange> changes)
    {
        return new EditRequest(Key, changes);
    }
}
