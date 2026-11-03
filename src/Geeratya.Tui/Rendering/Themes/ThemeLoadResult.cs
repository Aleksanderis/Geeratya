namespace Geeratya.Tui.Rendering.Themes;

/// <summary>A theme, or the reasons it could not be used (one per problem).</summary>
public sealed record ThemeLoadResult(Theme? Theme, IReadOnlyList<string> Errors);
