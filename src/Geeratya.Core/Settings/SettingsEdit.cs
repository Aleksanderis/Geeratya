namespace Geeratya.Core.Settings;

/// <summary>The outcome of a text edit of <c>settings.json</c>: the new text, or why the file was left alone.</summary>
/// <param name="Text">The edited text, or null when refused.</param>
/// <param name="Error">The reason, naming the line when the file does not parse; null on success.</param>
public sealed record SettingsEdit(string? Text, string? Error);
