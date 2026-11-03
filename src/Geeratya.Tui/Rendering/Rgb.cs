namespace Geeratya.Tui.Rendering;

/// <summary>A 24-bit colour.</summary>
public readonly record struct Rgb(byte Red, byte Green, byte Blue)
{
    private const int ChannelMask = 255;
    private const int HexShiftRed = 16;
    private const int HexShiftGreen = 8;

    /// <summary>The colour <paramref name="amount"/> (0 to 1) of the way from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static Rgb Blend(Rgb from, Rgb to, double amount)
    {
        return new Rgb(Channel(from.Red, to.Red, amount), Channel(from.Green, to.Green, amount), Channel(from.Blue, to.Blue, amount));
    }

    private static byte Channel(byte from, byte to, double amount)
    {
        return (byte)Math.Round(from + ((to - from) * amount));
    }

    /// <summary>Builds a colour from <c>0xRRGGBB</c>.</summary>
    public static Rgb FromHex(int value)
    {
        return new Rgb(
            (byte)((value >> HexShiftRed) & ChannelMask),
            (byte)((value >> HexShiftGreen) & ChannelMask),
            (byte)(value & ChannelMask));
    }
}
