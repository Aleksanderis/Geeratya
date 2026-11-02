namespace Geeratya.Core.Forms;

/// <summary>One field the user changed, ready for the write that the form's target builds.</summary>
public sealed record FieldChange(string FieldId, FormFieldKind Kind, FormValue Value);
