namespace Geeratya.AnsiToSvg.Tests;

public sealed class AnsiToSvgTests
{
    private const string Esc = "\u001b";

    [Fact]
    public void Parse_TrueColorForegroundAndBackground_AppliesToFollowingCells()
    {
        var screen = AnsiScreen.Parse($"{Esc}[38;2;1;2;3;48;5;196mab{Esc}[0mc", 4, 1);

        Assert.Equal(0x010203, screen[0][0].Style.Foreground);
        Assert.Equal(0xff0000, screen[0][1].Style.Background);
        Assert.Equal(CellStyle.Plain, screen[0][2].Style);
    }

    [Fact]
    public void Parse_WideCharacter_TakesTwoColumnsAndLeavesNoGap()
    {
        var screen = AnsiScreen.Parse("中x", 4, 1);

        Assert.Equal(2, screen[0][0].Width);
        Assert.Equal(string.Empty, screen[0][1].Text);
        Assert.Equal("x", screen[0][2].Text);
    }

    [Fact]
    public void Parse_LongLineAndMissingRows_ArePaddedAndCutToTheGrid()
    {
        var screen = AnsiScreen.Parse("abcdef", 3, 2);

        Assert.Equal(2, screen.Length);
        Assert.Equal(3, screen[0].Length);
        Assert.Equal("c", screen[0][2].Text);
        Assert.Equal(" ", screen[1][0].Text);
    }

    [Fact]
    public void Parse_BackgroundActiveAtLineEnd_PaintsTheRestOfTheRow()
    {
        var screen = AnsiScreen.Parse($"{Esc}[44ma", 3, 1);

        Assert.Equal(AnsiPalette.FromIndex(4), screen[0][2].Style.Background);
    }

    [Fact]
    public void Parse_UnknownSequencesAndTruncatedEscape_AreSkipped()
    {
        var screen = AnsiScreen.Parse($"{Esc}[2Jab{Esc}[", 4, 1);

        Assert.Equal("a", screen[0][0].Text);
        Assert.Equal("b", screen[0][1].Text);
    }

    [Fact]
    public void Write_ReverseAndMarkup_SwapsColoursAndEscapesText()
    {
        var screen = AnsiScreen.Parse($"{Esc}[7m<{Esc}[0m", 2, 1);
        var svg = SvgWriter.Write(screen, SvgTheme.Dark);

        Assert.Contains("&lt;", svg, StringComparison.Ordinal);
        Assert.Contains("<rect x=\"0\" y=\"0\" width=\"10\" height=\"20\" fill=\"#d4d4d4\"/>", svg, StringComparison.Ordinal);
        Assert.Contains("fill=\"#1e1e1e\">&lt;", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_LightTheme_UsesLightBackground()
    {
        var svg = SvgWriter.Write(AnsiScreen.Parse("a", 1, 1), SvgTheme.Light);

        Assert.Contains("fill=\"#ffffff\"", svg, StringComparison.Ordinal);
        Assert.Contains("fill=\"#1f2328\">a", svg, StringComparison.Ordinal);
    }
}
