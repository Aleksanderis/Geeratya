using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class TextWidthTests
{
    [Theory]
    [InlineData("abc", 3)]
    [InlineData("日本", 4)]
    [InlineData("é", 1)]
    [InlineData("", 0)]
    [InlineData("🚀", 2)]
    [InlineData("⚡", 2)]
    [InlineData("🇱🇹", 2)]
    [InlineData("👨‍👩‍👧", 2)]
    [InlineData("❤️", 2)]
    public void GivenText_WhenMeasuring_ThenWideTakesTwoAndCombiningTakesNone(string text, int expected)
    {
        Assert.Equal(expected, TextWidth.Of(text));
    }

    [Fact]
    public void GivenWideCharacterAtTheCut_WhenFitting_ThenItIsDroppedNotSplit()
    {
        Assert.Equal("a", TextWidth.Fit("a日", 2));
        Assert.Equal("a日", TextWidth.Fit("a日", 3));
    }

    [Fact]
    public void GivenWideCharacterAtTheCut_WhenPadding_ThenResultHasExactWidth()
    {
        Assert.Equal(2, TextWidth.Of(TextWidth.Pad("a日", 2)));
        Assert.Equal(6, TextWidth.Of(TextWidth.Pad("ab", 6)));
        Assert.Equal(string.Empty, TextWidth.Pad("abc", 0));
    }

    [Fact]
    public void GivenJoinedEmojiAtTheCut_WhenFitting_ThenTheSequenceIsKeptWholeOrDropped()
    {
        const string family = "👨\u200D👩\u200D👧";

        Assert.Equal("a", TextWidth.Fit($"a{family}", 2));
        Assert.Equal($"a{family}", TextWidth.Fit($"a{family}", 3));
    }
}
