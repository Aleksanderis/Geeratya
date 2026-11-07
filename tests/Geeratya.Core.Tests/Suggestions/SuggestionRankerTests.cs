using Geeratya.Core.Suggestions;
using Geeratya.Jira.Suggestions.Models;

namespace Geeratya.Core.Tests.Suggestions;

[Trait("Os", "Independent")]
public class SuggestionRankerTests
{
    private static readonly string[] _none = [];

    [Fact]
    public void GivenPrefixSubstringAndScatteredMatches_WhenRanking_ThenPrefixThenContainedThenScattered()
    {
        var pool = new[] { Label("b-a-c-k"), Label("the-back"), Label("backend"), Label("frontend") };

        var ranked = SuggestionRanker.Rank(pool, "back", _none, false);

        Assert.Equal(["backend", "the-back", "b-a-c-k"], ranked.Select(item => item.Label));
    }

    [Fact]
    public void GivenAnExactLabelAfterALongerOne_WhenRanking_ThenTheExactOneIsFirst()
    {
        var ranked = SuggestionRanker.Rank([Label("bugfix"), Label("Bug")], "bug", _none, true);

        Assert.Equal(["Bug", "bugfix"], ranked.Select(item => item.Label));
    }

    [Fact]
    public void GivenAnEmptyQuery_WhenRanking_ThenNothingMatches()
    {
        Assert.Empty(SuggestionRanker.Rank([Label("a")], "  ", _none, true));
    }

    [Fact]
    public void GivenAnExcludedId_WhenRanking_ThenItIsLeftOut()
    {
        var ranked = SuggestionRanker.Rank([Label("one"), Label("only")], "on", ["one"], false);

        Assert.Equal(["only"], ranked.Select(item => item.Label));
    }

    [Fact]
    public void GivenFreeTextAllowed_WhenNoLabelIsExactlyTheQuery_ThenTheTypedTextIsOfferedLast()
    {
        var ranked = SuggestionRanker.Rank([Label("backend")], "back", _none, true);

        Assert.Equal("back", ranked[^1].Label);
        Assert.Equal(SuggestionRanker.NewValueDetail, ranked[^1].Detail);
        Assert.Equal("back (new)", SuggestionText.Row(ranked[^1]));
    }

    [Fact]
    public void GivenAnExactLabel_WhenRankingWithFreeText_ThenNoNewRowIsOffered()
    {
        var ranked = SuggestionRanker.Rank([Label("Backend")], "backend", _none, true);

        Assert.Equal(["Backend"], ranked.Select(item => item.Label));
    }

    [Fact]
    public void GivenAnIssueCandidate_WhenTheQueryIsItsKey_ThenItMatchesOnTheDetail()
    {
        var issue = new Suggestion("GCM-12", "Fix login", "GCM-12", false);

        var ranked = SuggestionRanker.Rank([issue], "gcm-1", _none, false);

        Assert.Equal("GCM-12  Fix login", SuggestionText.Of(Assert.Single(ranked)));
    }

    [Fact]
    public void GivenWideCharacterNames_WhenRanking_ThenTheyMatchWithoutSplittingTheText()
    {
        var pool = new[] { Label("日本語チーム"), Label("team") };

        var ranked = SuggestionRanker.Rank(pool, "日本", _none, false);

        Assert.Equal(["日本語チーム"], ranked.Select(item => item.Label));
    }

    [Fact]
    public void GivenManyMatches_WhenRanking_ThenAtMostTwentyAreKept()
    {
        var pool = Enumerable.Range(0, 50).Select(index => Label($"label-{index}")).ToArray();

        Assert.Equal(20, SuggestionRanker.Rank(pool, "label", _none, false).Count);
    }

    [Fact]
    public void GivenAnInactiveUser_WhenFormatted_ThenItIsMarked()
    {
        var user = new Suggestion("acc-1", "Old Timer", null, true);

        Assert.Equal("Old Timer (inactive)", SuggestionText.Of(user));
    }

    [Theory]
    [InlineData("Epic")]
    [InlineData("Sub-task")]
    [InlineData("Release blocker")]
    public void GivenIssueWithType_WhenReadingTheRow_ThenTheTypeIsNamedBetweenKeyAndSummary(string kind)
    {
        var issue = new Suggestion("GCM-12", "Fix login", "GCM-12", false, kind);

        Assert.Equal($"GCM-12  [{kind}]  Fix login", SuggestionText.Row(issue));
        Assert.Equal("GCM-12  Fix login", SuggestionText.Of(issue));
    }

    [Fact]
    public void GivenIssueWithoutType_WhenReadingTheRow_ThenKeyAndSummaryOnly()
    {
        Assert.Equal("GCM-12  Fix login", SuggestionText.Row(new Suggestion("GCM-12", "Fix login", "GCM-12", false)));
    }

    private static Suggestion Label(string text)
    {
        return new Suggestion(text, text, null, false);
    }
}
