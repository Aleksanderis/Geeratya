using System.Globalization;

namespace Geeratya.Core.Keymap;

/// <summary>
/// The spelling of keys in the keymap and in key events: a single character (<c>j</c>, <c>?</c>, <c>G</c>) or a
/// name (<c>Esc</c>, <c>Enter</c>, <c>Down</c>), optionally prefixed with <c>Ctrl-</c> or <c>Alt-</c>.
/// </summary>
public static class KeyNotation
{
    public const string Space = "Space";

    private const string CtrlPrefix = "Ctrl-";
    private const string AltPrefix = "Alt-";

    private static readonly string[] _keyNames =
    [
        "Esc", "Enter", "Backspace", "Tab", "Shift-Tab", Space, "Up", "Down", "Left", "Right",
        "Home", "End", "PageUp", "PageDown", "Delete",
    ];

    /// <summary>Whether one key (not a sequence) is spelled correctly.</summary>
    public static bool IsValid(string key)
    {
        if (key.StartsWith(CtrlPrefix, StringComparison.Ordinal))
        {
            return IsValidBase(key[CtrlPrefix.Length..]);
        }

        if (key.StartsWith(AltPrefix, StringComparison.Ordinal))
        {
            return IsValidBase(key[AltPrefix.Length..]);
        }

        return IsValidBase(key);
    }

    /// <summary>The text a key types, or null when the key is not printable (names, modifier combinations).</summary>
    public static string? ToText(string key)
    {
        if (key == Space)
        {
            return " ";
        }

        return IsSingleCharacter(key) ? key : null;
    }

    private static bool IsValidBase(string key)
    {
        return IsSingleCharacter(key) || _keyNames.Contains(key);
    }

    private static bool IsSingleCharacter(string key)
    {
        if (key.Length == 0 || char.IsWhiteSpace(key[0]))
        {
            return false;
        }

        var enumerator = StringInfo.GetTextElementEnumerator(key);

        return enumerator.MoveNext() && enumerator.ElementIndex + enumerator.GetTextElement().Length == key.Length;
    }
}
