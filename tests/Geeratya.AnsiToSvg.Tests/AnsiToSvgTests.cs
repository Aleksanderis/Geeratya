namespace Geeratya.AnsiToSvg.Tests;

[Trait("Os", "Independent")]
public sealed class AnsiToSvgTests
{
    private static readonly string _escape = char.ToString((char)27);

    [Fact]
    public void GivenTrueColorAndIndexedColor_WhenParsed_ThenFollowingCellsCarryThem()
    {
        var screen = AnsiScreen.Parse($"{_escape}[38;2;1;2;3;48;5;196mab{_escape}[0mc", 4, 1);

        Assert.Equal(0x010203, screen[0][0].Style.Foreground);
        Assert.Equal(0xff0000, screen[0][1].Style.Background);
        Assert.Equal(CellStyle.Plain, screen[0][2].Style);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GivenHyperlinkSequences_WhenParsed_ThenOnlyTheLinkTextIsShown(bool stTerminated)
    {
        var terminator = stTerminated ? $"{_escape}\\" : "\a";
        var screen = AnsiScreen.Parse($"{_escape}]8;;https://example.com{terminator}ab{_escape}]8;;{_escape}\\c", 6, 1);

        Assert.Equal("a", screen[0][0].Text);
        Assert.Equal("b", screen[0][1].Text);
        Assert.Equal("c", screen[0][2].Text);
    }

    [Fact]
    public void GivenWideCharacter_WhenParsed_ThenItTakesTwoColumnsWithoutGap()
    {
        var screen = AnsiScreen.Parse("中x", 4, 1);

        Assert.Equal(2, screen[0][0].Width);
        Assert.Equal(string.Empty, screen[0][1].Text);
        Assert.Equal("x", screen[0][2].Text);
    }

    [Fact]
    public void GivenLongLineAndMissingRows_WhenParsed_ThenGridIsCutAndPadded()
    {
        var screen = AnsiScreen.Parse("abcdef", 3, 2);

        Assert.Equal(2, screen.Length);
        Assert.Equal(3, screen[0].Length);
        Assert.Equal("c", screen[0][2].Text);
        Assert.Equal(" ", screen[1][0].Text);
    }

    [Fact]
    public void GivenBackgroundActiveAtLineEnd_WhenParsed_ThenRestOfRowIsPainted()
    {
        var screen = AnsiScreen.Parse($"{_escape}[44ma", 3, 1);

        Assert.Equal(AnsiPalette.FromIndex(4), screen[0][2].Style.Background);
    }

    [Fact]
    public void GivenUnknownSequenceAndTruncatedEscape_WhenParsed_ThenTheyAreSkipped()
    {
        var screen = AnsiScreen.Parse($"{_escape}[2Jab{_escape}[", 4, 1);

        Assert.Equal("a", screen[0][0].Text);
        Assert.Equal("b", screen[0][1].Text);
    }

    [Fact]
    public void GivenAttributes_WhenParsed_ThenStyleFlagsAreSet()
    {
        var screen = AnsiScreen.Parse($"{_escape}[2;3;4ma", 1, 1);

        Assert.True(screen[0][0].Style.Dim);
        Assert.True(screen[0][0].Style.Italic);
        Assert.True(screen[0][0].Style.Underline);
    }

    [Fact]
    public void GivenStrikeAttribute_WhenParsedAndWritten_ThenTheSvgHasALineThrough()
    {
        var screen = AnsiScreen.Parse($"{_escape}[9ma{_escape}[29mb", 2, 1);

        Assert.True(screen[0][0].Style.Strike);
        Assert.False(screen[0][1].Style.Strike);
        Assert.Contains("line-through", SvgWriter.Write(screen, SvgTheme.Dark));
    }

    [Theory]
    [InlineData(16, 0x000000)]
    [InlineData(196, 0xff0000)]
    [InlineData(21, 0x0000ff)]
    [InlineData(232, 0x080808)]
    [InlineData(255, 0xeeeeee)]
    public void GivenPaletteIndex_WhenConverted_ThenCubeAndGrayRampMatchXterm(int index, int expected)
    {
        Assert.Equal(expected, AnsiPalette.FromIndex(index));
    }

    [Fact]
    public void GivenReverseAndMarkup_WhenWritten_ThenColoursSwapAndTextIsEscaped()
    {
        var screen = AnsiScreen.Parse($"{_escape}[7m<{_escape}[0m", 2, 1);
        var svg = SvgWriter.Write(screen, SvgTheme.Dark);

        Assert.Contains("&lt;", svg, StringComparison.Ordinal);
        Assert.Contains("<rect x=\"0\" y=\"0\" width=\"10\"", svg, StringComparison.Ordinal);
        Assert.Contains("fill=\"#d4d4d4\"/>", svg, StringComparison.Ordinal);
        Assert.Contains("fill=\"#1e1e1e\">&lt;", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenLightTheme_WhenWritten_ThenDefaultColoursAreLight()
    {
        var svg = SvgWriter.Write(AnsiScreen.Parse("a", 1, 1), SvgTheme.Light);

        Assert.Contains("fill=\"#ffffff\"", svg, StringComparison.Ordinal);
        Assert.Contains("fill=\"#1f2328\">a", svg, StringComparison.Ordinal);
    }
}
