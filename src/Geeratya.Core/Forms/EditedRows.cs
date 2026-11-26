namespace Geeratya.Core.Forms;

/// <summary>The lines of a field's text that its rows show while it is edited: <see cref="Count"/> lines from <see cref="First"/>.</summary>
/// <param name="First">The first line shown (counted by line breaks).</param>
/// <param name="Count">The number of lines shown, one row each.</param>
public sealed record EditedRows(int First, int Count);
