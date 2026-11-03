namespace Geeratya.Core.State;

/// <summary>
/// Switch to the theme called <paramref name="Name"/> (empty: list the themes); the host answers with
/// <see cref="ThemeApplied"/>. Core does not know the themes, the rendering layer does. A <paramref name="Preview"/>
/// only draws the theme (the settings screen's choice list): it is neither saved nor kept in the settings.
/// </summary>
public sealed record ApplyThemeEffect(string Name, bool Preview = false) : Effect;
