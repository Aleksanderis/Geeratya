namespace Geeratya.Tui.Rendering;

/// <summary>A 24-bit colour.</summary>
public readonly record struct Rgb(byte Red, byte Green, byte Blue)
{
    private const int ChannelMask = 255;
    private const int HexShiftRed = 16;
    private const int HexShiftGreen = 8;

    /// <summary>Builds a colour from <c>0xRRGGBB</c>.</summary>
    public static Rgb FromHex(int value)
    {
        return new Rgb(
            (byte)((value >> HexShiftRed) & ChannelMask),
            (byte)((value >> HexShiftGreen) & ChannelMask),
            (byte)(value & ChannelMask));
    }
}
