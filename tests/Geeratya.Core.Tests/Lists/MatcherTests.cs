using Geeratya.Core.Lists;

namespace Geeratya.Core.Tests.Lists;

[Trait("Os", "Independent")]
public class MatcherTests
{
    [Theory]
    [InlineData("Platform board", "PLAT", true)]
    [InlineData("Platform board", "form b", true)]
    [InlineData("Platform board", "pb", false)]
    [InlineData("anything", "", true)]
    public void GivenAQuery_WhenMatching_ThenItIsACaseInsensitiveSubstring(string text, string query, bool expected)
    {
        Assert.Equal(expected, Matcher.Matches(text, query));
    }

    [Fact]
    public void GivenQueries_WhenRanking_ThenExactBeatsPrefixBeatsSubstringBeatsLettersInOrder()
    {
        var exact = Matcher.Rank("bug", "bug");
        var prefix = Matcher.Rank("bugfix", "bug");
        var early = Matcher.Rank("a bug", "bug");
        var late = Matcher.Rank("a long bug", "bug");
        var scattered = Matcher.Rank("b u g", "bug");

        Assert.True(exact < prefix);
        Assert.True(prefix < early);
        Assert.True(early < late);
        Assert.True(late < scattered);
        Assert.Equal(-1, Matcher.Rank("bu", "bug"));
    }

    [Fact]
    public void GivenLetters_WhenMeasuringTheSpread_ThenTheTighterStretchIsShorter()
    {
        Assert.Equal(3, Matcher.Spread("bug", "xbugx"));
        Assert.Equal(5, Matcher.Spread("bug", "b-u-g"));
        Assert.Null(Matcher.Spread("gub", "bug"));
    }
}
