namespace Geeratya.Core.Forms;

/// <summary>
/// One field the user changed, ready for the write that the form's target builds. <paramref name="Initial"/> is the
/// value the form was opened with, so a multi-value field can be written as adds and removes and never replaces what
/// someone else changed meanwhile; null means nothing was known. <paramref name="Display"/> holds the labels of the
/// chosen options of a choice field (the priority's name, the assignee's display name), for the row patch.
/// </summary>
public sealed record FieldChange(
    string FieldId,
    FormFieldKind Kind,
    FormValue Value,
    FormValue? Initial = null,
    IReadOnlyList<string>? Display = null);
