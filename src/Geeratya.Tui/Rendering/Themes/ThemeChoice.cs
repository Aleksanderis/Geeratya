namespace Geeratya.Tui.Rendering.Themes;

/// <summary>
/// The outcome of <c>:theme [name]</c>: the theme to draw (null when nothing changes), the name to report and the
/// status line text.
/// </summary>
public sealed record ThemeChoice(Theme? Theme, string Name, string Message);
