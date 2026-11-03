using Geeratya.Tui.Rendering;
using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Geeratya.Tui.Views;

/// <summary>Turns a resolved palette style into a Terminal.Gui attribute.</summary>
public static class AttributeFactory
{
    public static Attribute Create(ResolvedStyle style)
    {
        var foreground = ToColor(style.Foreground, style.Foreground16);
        var background = style.DefaultBackground ? Color.None : ToColor(style.Background, style.Background16);

        return new Attribute(foreground, background, ToTextStyle(style.Style));
    }

    private static Color ToColor(Rgb? rgb, Ansi16Color? named)
    {
        if (named is { } ansi)
        {
            return new Color((ColorName16)(int)ansi);
        }

        if (rgb is { } value)
        {
            return new Color(value.Red, value.Green, value.Blue);
        }

        return new Color(ColorName16.Gray);
    }

    private static TextStyle ToTextStyle(RoleStyle style)
    {
        var result = TextStyle.None;

        if (style.HasFlag(RoleStyle.Bold))
        {
            result |= TextStyle.Bold;
        }

        if (style.HasFlag(RoleStyle.Underline))
        {
            result |= TextStyle.Underline;
        }

        if (style.HasFlag(RoleStyle.Reverse))
        {
            result |= TextStyle.Reverse;
        }

        if (style.HasFlag(RoleStyle.Italic))
        {
            result |= TextStyle.Italic;
        }

        if (style.HasFlag(RoleStyle.Faint))
        {
            result |= TextStyle.Faint;
        }

        return result;
    }
}
