using Geeratya.Core.Suggestions;
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
/// <param name="Error">Why the field is wrong (checked locally or refused by Jira), or null.</param>
/// <param name="TakesTypedKey">A full issue key typed into this parent-kind field is taken as it stands, without
/// picking it from the popup (the link form). Enter takes the key, Tab the highlighted row.</param>
/// <param name="Lookup">The project list a typed filter also searches (components, versions of a create form), or null.</param>
public sealed record FormField(
    string Id,
    string Label,
    FormFieldKind Kind,
    bool Required,
    IReadOnlyList<FieldOption> Options,
    FormValue Value,
    FormValue Initial,
    string Entry,
    string? Error,
    SuggestionKind? Lookup = null,
    bool TakesTypedKey = false)
{
    public bool IsEditable => FormFieldKinds.IsEditable(Kind);

    /// <summary>True when the user changed the field, counting a label that is typed and not yet added, or text typed into a person or parent field and not picked.</summary>
    public bool IsChanged
    {
        get
        {
            var typed = Kind is FormFieldKind.Labels or FormFieldKind.User or FormFieldKind.Parent && Entry.Length > 0;

            return IsEditable && (!Value.Equals(Initial) || typed);
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
            null);
    }

    /// <summary>The field opened with <paramref name="value"/> as both its value and its initial value.</summary>
    public FormField WithInitial(FormValue value)
    {
        return this with { Value = value, Initial = value };
    }
}
