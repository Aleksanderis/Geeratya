namespace Geeratya.Core.State;

/// <summary>
/// Switch to the theme called <paramref name="Name"/> (empty: list the themes); the host answers with
/// <see cref="ThemeApplied"/>. Core does not know the themes, the rendering layer does.
/// </summary>
public sealed record ApplyThemeEffect(string Name) : Effect;
