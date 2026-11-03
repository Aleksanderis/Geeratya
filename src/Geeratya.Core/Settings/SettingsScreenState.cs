namespace Geeratya.Core.Settings;

/// <summary>The open settings screen (<c>:settings</c>), a full-body screen over the active site in Normal mode.</summary>
/// <param name="Selected">The highlighted row, an index into <see cref="SettingsRows.Build"/>; clamped when read.</param>
/// <param name="Choice">The highlighted word of the open choice list of the selected row, or null when no list is open.</param>
/// <param name="Preview">The theme currently drawn only as a preview (not saved), or null.</param>
public sealed record SettingsScreenState(int Selected, int? Choice = null, string? Preview = null);
