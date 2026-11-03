namespace Geeratya.Tui.Rendering.Themes;

/// <summary>The JSON shape of a theme file; values are checked by <see cref="ThemeFileParser"/>.</summary>
public sealed record ThemeFileModel(
    int? Version,
    string? Variant,
    Dictionary<string, string>? Colors,
    Dictionary<string, string>? Ansi);
