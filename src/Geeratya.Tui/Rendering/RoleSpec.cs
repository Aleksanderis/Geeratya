using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Rendering;

/// <summary>Which theme colours draw a role, its no-colour text attributes and how it is grounded at 16 colours.</summary>
internal sealed record RoleSpec(
    ThemeColor Foreground,
    ThemeColor Background,
    RoleStyle Style,
    RoleGround Ground = RoleGround.Plain);
