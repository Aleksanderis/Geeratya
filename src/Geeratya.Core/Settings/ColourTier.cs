namespace Geeratya.Core.Settings;

/// <summary>How many colours the terminal shows; the host decides it at start and the settings screen names it.</summary>
public enum ColourTier
{
    /// <summary>24-bit colour: every theme is drawn as designed.</summary>
    TrueColour,

    /// <summary>The 16 terminal colours: theme colours are approximated.</summary>
    Basic16,

    /// <summary>No colour (NO_COLOR, a dumb terminal): themes change nothing.</summary>
    None,
}
