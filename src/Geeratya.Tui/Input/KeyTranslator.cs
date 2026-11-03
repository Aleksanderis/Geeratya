using System.Text;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;

namespace Geeratya.Tui.Input;

/// <summary>Turns a Terminal.Gui key into the renderer-agnostic spelling of <c>KeyNotation</c>.</summary>
public static class KeyTranslator
{
    /// <summary>The key spelling, or null for keys the keymap cannot name (modifiers alone, function keys).</summary>
    public static string? Translate(Key key)
    {
        if (key.IsModifierOnly)
        {
            return null;
        }

        var code = key.KeyCode & ~(KeyCode.ShiftMask | KeyCode.AltMask | KeyCode.CtrlMask);
        var name = NameOf(code);

        if (name == "Enter" && key.IsCtrl && !key.IsAlt)
        {
            // Ctrl-j is the line feed (0x0A); Terminal.Gui reports it as Ctrl+Enter, distinct from Enter (0x0D).
            return "Ctrl-j";
        }

        if (name is not null)
        {
            return WithModifiers(key, name, name == "Tab");
        }

        if (!Rune.IsValid((int)(code & KeyCode.CharMask)) || (code & KeyCode.CharMask) == KeyCode.Null)
        {
            return null;
        }

        var text = new Rune((int)(code & KeyCode.CharMask)).ToString();

        if (key.IsCtrl || key.IsAlt)
        {
            return WithModifiers(key, text.ToLowerInvariant(), false);
        }

        return IsUpperLetter(code) && !key.IsShift ? text.ToLowerInvariant() : text;
    }

    private static bool IsUpperLetter(KeyCode code)
    {
        return code is >= KeyCode.A and <= KeyCode.Z;
    }

    private static string WithModifiers(Key key, string name, bool allowShift)
    {
        var prefix = string.Empty;

        if (key.IsCtrl)
        {
            prefix = $"{prefix}Ctrl-";
        }

        if (key.IsAlt)
        {
            prefix = $"{prefix}Alt-";
        }

        if (key.IsShift && allowShift)
        {
            prefix = $"{prefix}Shift-";
        }

        return $"{prefix}{name}";
    }

    private static string? NameOf(KeyCode code)
    {
        return code switch
        {
            KeyCode.Esc => "Esc",
            KeyCode.Enter => "Enter",
            KeyCode.Backspace => "Backspace",
            KeyCode.Tab => "Tab",
            KeyCode.Space => "Space",
            KeyCode.CursorUp => "Up",
            KeyCode.CursorDown => "Down",
            KeyCode.CursorLeft => "Left",
            KeyCode.CursorRight => "Right",
            KeyCode.Home => "Home",
            KeyCode.End => "End",
            KeyCode.PageUp => "PageUp",
            KeyCode.PageDown => "PageDown",
            KeyCode.Delete => "Delete",
            _ => null,
        };
    }
}
