using Geeratya.Jira.Transitions.Models;

namespace Geeratya.Core.Forms;

/// <summary>
/// One field of a form. The field list, the kinds, the required flags and the options come only from the metadata
/// Jira returned for the project and issue type, never from code.
/// </summary>
/// <param name="Id">The field id to write under.</param>
/// <param name="Label">The name Jira shows.</param>
/// <param name="Kind">How the field is edited.</param>
/// <param name="Required">Jira refuses the write without a value.</param>
/// <param name="Options">The allowed values of a choice field.</param>
/// <param name="Value">The value as edited.</param>
/// <param name="Initial">The value the form was opened with; only fields that differ are written.</param>
/// <param name="Entry">The typing buffer: the filter of a choice field, the label being typed.</param>
/// <param name="Cursor">The highlighted row of a choice field's visible options.</param>
/// <param name="Error">Why the field is wrong (checked locally or refused by Jira), or null.</param>
public sealed record FormField(
    string Id,
    string Label,
    FormFieldKind Kind,
    bool Required,
    IReadOnlyList<FieldOption> Options,
    FormValue Value,
    FormValue Initial,
    string Entry,
    int Cursor,
    string? Error)
{
    /// <summary>The label of the first row of a single choice that is not required, which clears the value.</summary>
    public string NoneLabel { get; init; } = "(none)";

    /// <summary>
    /// The options come from a search of the typed text (people), not from the metadata: typing asks for a new list
    /// and the list is not filtered again here.
    /// </summary>
    public bool Searchable { get; init; }

    /// <summary>
    /// The typed text of a searchable field has been sent to the search and the answer is not in yet, so the options
    /// shown belong to older text and Enter must not choose from them.
    /// </summary>
    public bool Searching { get; init; }

    /// <summary>What a searchable field lists while nothing is typed.</summary>
    public IReadOnlyList<FieldOption> Suggested { get; init; } = [];

    public bool IsEditable => FormFieldKinds.IsEditable(Kind);

    /// <summary>True when the user changed the field, counting a label that is typed and not yet added.</summary>
    public bool IsChanged
    {
        get
        {
            var typedLabel = Kind == FormFieldKind.Labels && Entry.Length > 0;

            return IsEditable && (!Value.Equals(Initial) || typedLabel);
        }
    }

    /// <summary>A field of <paramref name="kind"/> with no value.</summary>
    public static FormField Create(
        string id,
        string label,
        FormFieldKind kind,
        bool required,
        IReadOnlyList<FieldOption> options)
    {
        return new FormField(
            id,
            label,
            kind,
            required,
            options,
            FormValue.Empty,
            FormValue.Empty,
            string.Empty,
            0,
            null);
    }

    /// <summary>The field opened with <paramref name="value"/> as both its value and its initial value.</summary>
    public FormField WithInitial(FormValue value)
    {
        return this with { Value = value, Initial = value };
    }
}
