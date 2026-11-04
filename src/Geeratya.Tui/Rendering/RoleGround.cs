namespace Geeratya.Tui.Rendering;

/// <summary>How a role is drawn at 16 colours.</summary>
internal enum RoleGround
{
    /// <summary>The role's own colour on the variant's black or white.</summary>
    Plain,

    /// <summary>White on blue: the selection, whatever the terminal's colours.</summary>
    OnBlue,

    /// <summary>
    /// The status bar: white on blue on a dark theme, white on black on a light one (the light table has no bright
    /// colour that reads on blue, so the hint key could not be told from its label, #684). Keys are <see cref="OnBarKey"/>.
    /// </summary>
    OnBar,

    /// <summary>The bar of <see cref="OnBar"/> with a hint key on it: bright yellow, the one colour that reads on both bars.</summary>
    OnBarKey,

    /// <summary>The brightest text colour of the variant (strong text).</summary>
    Strong,

    /// <summary>A filled chip: the variant's ground colour on the role's own colour (spec background).</summary>
    Chip,

    /// <summary>Reverse video of the variant's ground: the Visual range.</summary>
    Range,
}
