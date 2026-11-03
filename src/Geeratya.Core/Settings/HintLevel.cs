namespace Geeratya.Core.Settings;

/// <summary>
/// How much the status bar hints show (<c>:set hints.statusBar</c>). A level hides hints, it never disables a key.
/// </summary>
public enum HintLevel
{
    /// <summary>The main keys of the screen and the global ones.</summary>
    Full,

    /// <summary>Only <c>? help  : commands  q quit</c>.</summary>
    Minimal,

    /// <summary>No hints.</summary>
    Off,
}
