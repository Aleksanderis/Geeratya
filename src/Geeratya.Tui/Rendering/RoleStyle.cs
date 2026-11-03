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

    /// <summary>Dimmed text (SGR 2): the view behind an overlay at 16 colours.</summary>
    Faint = 16,

    /// <summary>
    /// Underline in the 16-colour and no-colour tiers only: a role whose 24-bit tier marks itself with a tint (the
    /// of a pane without focus).
    /// </summary>
    UnderlineWithoutTint = 32,
}
