namespace Geeratya.Core.CommandPalette;

/// <summary>What a palette list line is.</summary>
public enum PaletteRowKind
{
    /// <summary>A group subheader; never selectable.</summary>
    Header,

    /// <summary>An action: <c>Enter</c> runs its id exactly as its key would.</summary>
    Action,

    /// <summary>A palette-only command without a key; <c>Enter</c> runs its command line.</summary>
    Command,

    /// <summary>The usage line of a typed command (its name and a space were typed); not selectable.</summary>
    Usage,
}
