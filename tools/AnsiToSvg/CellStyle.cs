namespace Geeratya.AnsiToSvg;

/// <summary>Colours and attributes of one terminal cell; a colour of -1 means the terminal default.</summary>
public readonly record struct CellStyle(
    int Foreground,
    int Background,
    bool Bold,
    bool Dim,
    bool Italic,
    bool Underline,
    bool Reverse,
    bool Strike = false)
{
    public const int DefaultColor = -1;

    public static CellStyle Plain { get; } = new(DefaultColor, DefaultColor, false, false, false, false, false);
}
