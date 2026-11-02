namespace Geeratya.Tui.Rendering;

/// <summary>A loaded theme, or why it was refused (a theme with any error is never used half way).</summary>
public sealed record ThemeLoadResult(Theme? Theme, IReadOnlyList<string> Errors);
