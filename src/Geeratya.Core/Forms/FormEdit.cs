namespace Geeratya.Core.Forms;

/// <summary>The result of accepting a field with Enter: the field, and whether the focus moves on.</summary>
public sealed record FormEdit(FormField Field, bool Advance);
