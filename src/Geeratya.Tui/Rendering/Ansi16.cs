namespace Geeratya.Tui.Rendering;

/// <summary>
/// Typical RGB values of the 16 terminal colours (xterm), used to test contrast of the 16-colour tier.
/// </summary>
public static class Ansi16
{
    private static readonly Rgb[] _values =
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

    public static Rgb ToRgb(Ansi16Color color)
    {
        return _values[(int)color];
    }
}
