namespace Geeratya.Tui.Rendering;

/// <summary>Text attributes that carry meaning when there are no colours (<c>NO_COLOR</c>).</summary>
[Flags]
public enum RoleStyle
{
    None = 0,
    Bold = 1,
    Underline = 2,
    Reverse = 4,
    Italic = 8,
}
