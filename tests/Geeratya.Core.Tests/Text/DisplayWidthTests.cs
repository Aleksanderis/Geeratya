using System.Text;
using Geeratya.Core.Text;

namespace Geeratya.Core.Tests.Text;

[Trait("Os", "Independent")]
public class DisplayWidthTests
{
    [Theory]
    [InlineData("abc", 3)]
    [InlineData("日本", 4)]
    [InlineData("한글", 4)]
    [InlineData("ＡＢ", 4)]
    [InlineData("é", 1)]
    [InlineData("é", 1)]
    [InlineData("", 0)]
    [InlineData("🚀", 2)]
    [InlineData("⚡", 2)]
    [InlineData("✅", 2)]
    [InlineData("🇱🇹", 2)]
    [InlineData("👨‍👩‍👧", 2)]
    [InlineData("❤️", 2)]
    [InlineData("🧑🏽", 2)]
    public void GivenText_WhenMeasuring_ThenWideTakesTwoAndCombiningTakesNone(string text, int expected)
    {
        Assert.Equal(expected, DisplayWidth.Of(text));
    }

    [Theory]
    [InlineData(0x1100, 0x115F)]
    [InlineData(0x3041, 0x3096)]
    [InlineData(0x4E00, 0x9FFF)]
    [InlineData(0xAC00, 0xD7A3)]
    [InlineData(0xFF01, 0xFF60)]
    [InlineData(0xFFE0, 0xFFE6)]
    [InlineData(0x1F300, 0x1F320)]
    [InlineData(0x1F680, 0x1F6C5)]
    [InlineData(0x20000, 0x2A6DF)]
    public void GivenWideRangeEnds_WhenMeasuring_ThenBothAreTwo(int first, int last)
    {
        Assert.True(DisplayWidth.IsWide(first));
        Assert.True(DisplayWidth.IsWide(last));
        Assert.Equal(2, DisplayWidth.Of(new Rune(first)));
        Assert.Equal(2, DisplayWidth.Of(new Rune(last)));
    }

    [Theory]
    [InlineData(0x41)]
    [InlineData(0x10FF)]
    [InlineData(0xD7A4)]
    [InlineData(0xFF61)]
    [InlineData(0xFFE7)]
    [InlineData(0x1F321)]
    [InlineData(0x40000)]
    public void GivenNarrowCodePoint_WhenMeasuring_ThenItIsOne(int codePoint)
    {
        Assert.False(DisplayWidth.IsWide(codePoint));
        Assert.Equal(1, DisplayWidth.Of(new Rune(codePoint)));
    }

    [Theory]
    [InlineData(0x0301)]
    [InlineData(0x200D)]
    [InlineData(0xFE0F)]
    [InlineData(0x0007)]
    public void GivenCombiningOrControl_WhenMeasuring_ThenItIsZero(int codePoint)
    {
        Assert.Equal(0, DisplayWidth.Of(new Rune(codePoint)));
    }
}
