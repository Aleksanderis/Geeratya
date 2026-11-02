namespace Geeratya.Tui.Rendering;

/// <summary>
/// The JSON of a user theme: an optional built-in theme to start from and the roles to change. A role may leave out
/// any of its parts; they come from the base theme.
/// </summary>
public sealed record ThemeFileModel(string? Base, Dictionary<string, ThemeRoleModel>? Roles);
