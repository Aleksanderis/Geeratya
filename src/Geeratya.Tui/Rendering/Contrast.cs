namespace Geeratya.Tui.Rendering;

/// <summary>WCAG contrast ratio between two colours.</summary>
public static class Contrast
{
    private const double RedWeight = 0.2126;
    private const double GreenWeight = 0.7152;
    private const double BlueWeight = 0.0722;
    private const double Offset = 0.05;
    private const double LinearThreshold = 0.03928;
    private const double LinearDivisor = 12.92;
    private const double GammaOffset = 0.055;
    private const double GammaScale = 1.055;
    private const double GammaPower = 2.4;
    private const double ChannelMax = 255.0;

    public static double Ratio(Rgb first, Rgb second)
    {
        var firstLuminance = Luminance(first);
        var secondLuminance = Luminance(second);
        var lighter = Math.Max(firstLuminance, secondLuminance);
        var darker = Math.Min(firstLuminance, secondLuminance);

        return (lighter + Offset) / (darker + Offset);
    }

    private static double Luminance(Rgb color)
    {
        var red = RedWeight * Linear(color.Red);
        var green = GreenWeight * Linear(color.Green);
        var blue = BlueWeight * Linear(color.Blue);

        return red + green + blue;
    }

    private static double Linear(byte channel)
    {
        var value = channel / ChannelMax;

        if (value <= LinearThreshold)
        {
            return value / LinearDivisor;
        }

        return Math.Pow((value + GammaOffset) / GammaScale, GammaPower);
    }
}
