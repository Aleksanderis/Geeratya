using Geeratya.Core.Text;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class StyledLinesTests
{
    private static string Flat(StyledLine line) => string.Concat(line.Spans.Select(span => span.Text));

    [Fact]
    public void GivenALineOfExactlyTheWidth_WhenFitting_ThenTheSameLineIsReturned()
    {
        var line = new StyledLine([new StyledSpan("日本", TextRole.Body), new StyledSpan("ab", TextRole.Muted)]);

        Assert.Same(line, StyledLines.Fit(line, 6));
    }

    [Fact]
    public void GivenAShorterLine_WhenFitting_ThenItIsPaddedInThePaddingRole()
    {
        var fitted = StyledLines.Fit(new StyledLine([new StyledSpan("ab", TextRole.Body)]), 5, TextRole.BarText);

        Assert.Equal("ab   ", Flat(fitted));
        Assert.Equal(TextRole.BarText, fitted.Spans[^1].Role);
    }

    [Theory]
    [InlineData(3, "ab ")]
    [InlineData(4, "ab日")]
    [InlineData(5, "ab日 ")]
    public void GivenALongerLine_WhenFitting_ThenAWideCharacterIsNotSplit(int width, string expected)
    {
        var fitted = StyledLines.Fit(new StyledLine([new StyledSpan("ab", TextRole.Body), new StyledSpan("日本", TextRole.Muted)]), width);

        Assert.Equal(expected, Flat(fitted));
    }

    [Fact]
    public void GivenALineWithAnEmptySpanAtTheWidth_WhenFitting_ThenTheEmptySpanIsDropped()
    {
        var line = new StyledLine([new StyledSpan("abc", TextRole.Body), new StyledSpan(string.Empty, TextRole.Muted)]);

        var fitted = StyledLines.Fit(line, 3);

        Assert.Single(fitted.Spans);
    }

    [Theory]
    [InlineData("abcdef", 2, 4, "cd")]
    [InlineData("日本語", 1, 5, " 本 ")]
    [InlineData("a日b", 0, 2, "a ")]
    [InlineData("éx", 0, 1, "é")]
    public void GivenASlice_WhenCutting_ThenAStraddlingWideCharacterBecomesASpace(string text, int from, int to, string expected)
    {
        var sliced = StyledLines.Slice(new StyledLine([new StyledSpan(text, TextRole.Body)]), from, to);

        Assert.Equal(expected, Flat(sliced));
    }
}
