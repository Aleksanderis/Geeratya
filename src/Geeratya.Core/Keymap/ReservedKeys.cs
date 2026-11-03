namespace Geeratya.Core.Keymap;

/// <summary>
/// Keys the keymap cannot change: <c>Ctrl-c</c> quits before the keymap is consulted (so a bad <c>keymap.json</c>
/// never locks the user in), except in Insert mode, where it cancels the edit and <c>Esc</c> and the palette still work.
/// </summary>
public static class ReservedKeys
{
    /// <summary>The key that always quits.</summary>
    public const string Quit = "Ctrl-c";

    /// <summary>True when <paramref name="keys"/> is the reserved quit key in <paramref name="mode"/>.</summary>
    public static bool IsReserved(InputMode mode, string keys)
    {
        return keys == Quit && mode != InputMode.Insert;
    }
}
