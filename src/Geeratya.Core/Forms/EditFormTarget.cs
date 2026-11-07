using Geeratya.Core.Writes;

namespace Geeratya.Core.Forms;

/// <summary>
/// Edit fields of one issue. <paramref name="Known"/> holds the current values the opener already has (from the row
/// or the detail), by field id; the metadata handler adds the values of the other editable fields from the issue
/// after editmeta (<see cref="EditKnownValues"/>). A field with an unknown current value would be overwritten blind, so only fields with a known value are offered when <paramref name="OnlyKnown"/> is set.
/// <paramref name="ReadOnlyIds"/> names fields shown read-only with a link to the browser, because editing them here
/// would lose content (a description that does not survive the Markdown-like text).
/// A choice field whose current value is only a name (the row shows the priority's name, not its id) is known as
/// <see cref="FormValue.OfText"/> of that name and is matched to the allowed value of that label.
/// <paramref name="Only"/> limits the form to one kind of field (the labels pilot), <paramref name="OnlyId"/> to one
/// field (the priority command). <paramref name="EstimateOnly"/> is the estimate command: the form shows the one
/// number field of the issue that the site or a board uses as its estimate, which the metadata handler picks (the id
/// is per site), and the write patches the row's estimate. <paramref name="EstimateFieldId"/> is the estimation field
/// of the board the tab shows: when set, the handler offers that field or none.
/// <paramref name="ParentLevel"/> is the hierarchy level a parent of this issue must have (<see cref="ParentLevels"/>), when the issue type is known.
/// </summary>
public sealed record EditFormTarget(
    string Key,
    IReadOnlyDictionary<string, FormValue> Known,
    bool OnlyKnown,
    FormFieldKind? Only,
    string? OnlyId = null,
    IReadOnlyCollection<string>? ReadOnlyIds = null,
    bool EstimateOnly = false,
    string? EstimateFieldId = null,
    int? ParentLevel = null) : FormTarget
{
    public override string Title => $"Edit {Key}";

    public override string IssueKey => Key;

    public override WriteRequest Request(IReadOnlyList<FieldChange> changes)
    {
        return new EditRequest(Key, changes, EstimateOnly);
    }
}
