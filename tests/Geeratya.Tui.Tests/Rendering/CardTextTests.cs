using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class CardTextTests
{
    [Fact]
    public void GivenShortSummary_WhenWrapping_ThenItStaysOnTheFirstLine()
    {
        var lines = CardText.TwoLines("Fix login", 20);

        Assert.Equal("Fix login", lines.First);
        Assert.Equal(string.Empty, lines.Second);
    }

    [Fact]
    public void GivenSummaryOverOneLine_WhenWrapping_ThenItBreaksAtTheLastSpaceThatFits()
    {
        var lines = CardText.TwoLines("Fix login redirect after token refresh", 20);

        Assert.Equal("Fix login redirect", lines.First);
        Assert.Equal("after token refresh", lines.Second);
    }

    [Fact]
    public void GivenSummaryOverTwoLines_WhenWrapping_ThenTheSecondLineEndsWithAnEllipsisWithinTheWidth()
    {
        var lines = CardText.TwoLines("Fix login redirect after token refresh and then some more words", 20);

        Assert.EndsWith("…", lines.Second);
        Assert.Equal(20, TextWidth.Of(lines.Second));
    }

    [Fact]
    public void GivenOneLongWord_WhenWrapping_ThenItIsCutAtTheWidthAndTheRestContinues()
    {
        var lines = CardText.TwoLines("Supercalifragilisticexpialidocious", 10);

        Assert.Equal("Supercalif", lines.First);
        Assert.Equal(10, TextWidth.Of(lines.Second));
        Assert.EndsWith("…", lines.Second);
    }

    [Fact]
    public void GivenWideCharacters_WhenWrapping_ThenNoCharacterIsSplitAndWidthHolds()
    {
        var lines = CardText.TwoLines("日本語日本語日本語日本語日本語", 9);

        Assert.Equal("日本語日", lines.First);
        Assert.True(TextWidth.Of(lines.Second) <= 9);
    }

    [Theory]
    [InlineData("", 10)]
    [InlineData("   ", 10)]
    [InlineData("anything", 0)]
    [InlineData("anything", -3)]
    public void GivenNothingToShowOrNoRoom_WhenWrapping_ThenBothLinesAreEmpty(string summary, int width)
    {
        var lines = CardText.TwoLines(summary, width);

        Assert.Equal(string.Empty, lines.First);
        Assert.Equal(string.Empty, lines.Second);
    }

    [Theory]
    [InlineData("abc", 3, "abc")]
    [InlineData("abcd", 3, "ab…")]
    [InlineData("abcd", 1, "a")]
    [InlineData("abcd", 0, "")]
    public void GivenText_WhenShortening_ThenItEndsWithAnEllipsisOnlyWhenCut(string text, int width, string expected)
    {
        Assert.Equal(expected, CardText.Shorten(text, width));
    }
}
