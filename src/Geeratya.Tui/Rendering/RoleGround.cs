namespace Geeratya.Tui.Rendering;

/// <summary>How a role is drawn at 16 colours.</summary>
internal enum RoleGround
{
    /// <summary>The role's own colour on the variant's black or white.</summary>
    Plain,

    /// <summary>White on blue: the status bar and the selection, whatever the terminal's colours.</summary>
    OnBlue,

    /// <summary>The brightest text colour of the variant (strong text).</summary>
    Strong,

    /// <summary>A filled chip: the variant's ground colour on the role's own colour (spec background).</summary>
    Chip,

    /// <summary>Reverse video of the variant's ground: the Visual range.</summary>
    Range,
}
