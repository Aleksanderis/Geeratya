using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

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

    /// <summary>
    /// The keys of a binding as the UI and the docs show them (the Gitunia labelling): the leader as
    /// <c>&lt;space&gt;</c>, a plain character run joined (<c>gg</c>, <c>&lt;space&gt;is</c>), a space only next to a
    /// long key name (<c>Ctrl-w c</c>), and a named key inside a sequence in angle brackets
    /// (<c>&lt;space&gt;&lt;Tab&gt;</c>). A single named key stays bare (<c>Esc</c>, <c>Enter</c>, <c>Down</c>).
    /// The config file keeps the stored spelling.
    /// </summary>
    [return: NotNullIfNotNull(nameof(keys))]
    public static string? Display(string? keys)
    {
        if (keys is null)
        {
            return null;
        }

        var parts = keys.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var tokens = parts.Select(part => DisplayToken(part, parts.Length > 1)).ToList();
        var builder = new StringBuilder();

        for (var index = 0; index < tokens.Count; index++)
        {
            if (index > 0 && (IsLong(tokens[index]) || IsLong(tokens[index - 1])))
            {
                builder.Append(' ');
            }

            builder.Append(tokens[index]);
        }

        return builder.ToString();
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

    private static string DisplayToken(string key, bool inSequence)
    {
        if (key == Space)
        {
            return "<space>";
        }

        var hasModifier = key.StartsWith(CtrlPrefix, StringComparison.Ordinal)
            || key.StartsWith(AltPrefix, StringComparison.Ordinal);
        var bare = hasModifier ? string.Empty : key;

        return inSequence && bare.Length > 1 && _keyNames.Contains(bare) ? $"<{bare}>" : key;
    }

    private static bool IsLong(string token)
    {
        var bracketed = token[0] == '<' && token[^1] == '>';

        return token.Length > 1 && token != "<space>" && !bracketed;
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
