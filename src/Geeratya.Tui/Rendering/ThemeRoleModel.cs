namespace Geeratya.Tui.Rendering;

/// <summary>
/// One role in a user theme file. Colours are <c>#RRGGBB</c>, 16-colour names are those of <see cref="Ansi16Color"/>.
/// </summary>
public sealed record ThemeRoleModel(
    string? Foreground,
    string? Background,
    string? Foreground16,
    string? Background16,
    string[]? Style);
