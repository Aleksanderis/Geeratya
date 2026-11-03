namespace Geeratya.Tui.Rendering.Themes;

/// <summary>The theme to start with and the notice for the status line, null when there is nothing to say.</summary>
public sealed record ThemeStart(Theme Theme, string? Notice);
