using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// Typical RGB values of the 16 terminal colours, used to test contrast of the 16-colour tier. The terminal's own
/// palette decides the real look: a dark terminal is approximated by the xterm values, a light one by a table with
/// darker normal colours (light terminal themes darken them so they read on white).
/// </summary>
public static class Ansi16
{
    private static readonly Rgb[] _dark =
    [
        new(0, 0, 0),
        new(0, 0, 238),
        new(0, 205, 0),
        new(0, 205, 205),
        new(205, 0, 0),
        new(205, 0, 205),
        new(205, 205, 0),
        new(229, 229, 229),
        new(127, 127, 127),
        new(92, 92, 255),
        new(0, 255, 0),
        new(0, 255, 255),
        new(255, 0, 0),
        new(255, 0, 255),
        new(255, 255, 0),
        new(255, 255, 255),
    ];

    private static readonly Rgb[] _light =
    [
        new(0, 0, 0),
        new(0, 0, 205),
        new(0, 128, 0),
        new(0, 128, 128),
        new(190, 0, 0),
        new(150, 0, 150),
        new(128, 100, 0),
        new(229, 229, 229),
        new(90, 90, 90),
        new(0, 0, 255),
        new(0, 160, 0),
        new(0, 160, 160),
        new(230, 0, 0),
        new(200, 0, 200),
        new(160, 130, 0),
        new(255, 255, 255),
    ];

    public static Rgb ToRgb(Ansi16Color color, ThemeVariant variant)
    {
        return variant == ThemeVariant.Light ? _light[(int)color] : _dark[(int)color];
    }
}
