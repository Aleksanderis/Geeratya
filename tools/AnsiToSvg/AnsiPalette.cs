namespace Geeratya.AnsiToSvg;

/// <summary>The xterm 256-colour table as 0xRRGGBB values.</summary>
public static class AnsiPalette
{
    private const int SixteenColors = 16;
    private const int CubeStart = 16;
    private const int GrayStart = 232;
    private const int CubeSize = 6;
    private const int CubeBase = 55;
    private const int CubeStep = 40;
    private const int GrayBase = 8;
    private const int GrayStep = 10;
    private const int ByteShift = 8;

    private static readonly int[] _sixteen =
    [
        0x000000, 0xcd3131, 0x0dbc79, 0xe5e510, 0x2472c8, 0xbc3fbc, 0x11a8cd, 0xe5e5e5,
        0x666666, 0xf14c4c, 0x23d18b, 0xf5f543, 0x3b8eea, 0xd670d6, 0x29b8db, 0xffffff
    ];

    public static int FromIndex(int index)
    {
        if (index < SixteenColors)
        {
            return _sixteen[Math.Max(index, 0)];
        }

        if (index >= GrayStart)
        {
            var level = Math.Min(GrayBase + ((index - GrayStart) * GrayStep), byte.MaxValue);

            return FromRgb(level, level, level);
        }

        var offset = index - CubeStart;

        return FromRgb(
            CubeLevel(offset / (CubeSize * CubeSize) % CubeSize),
            CubeLevel(offset / CubeSize % CubeSize),
            CubeLevel(offset % CubeSize));
    }

    public static int FromRgb(int red, int green, int blue)
    {
        return (Clamp(red) << (ByteShift * 2)) | (Clamp(green) << ByteShift) | Clamp(blue);
    }

    private static int CubeLevel(int step)
    {
        return step == 0 ? 0 : CubeBase + (step * CubeStep);
    }

    private static int Clamp(int value)
    {
        return Math.Clamp(value, 0, byte.MaxValue);
    }
}
