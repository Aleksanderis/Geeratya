using Geeratya.Jira.Suggestions;
using Geeratya.Jira.Suggestions.Models;

namespace Geeratya.Jira.Tests.Suggestions;

public sealed class SuggestionFilterTests
{
    [Fact]
    public void GivenMoreThanTwentyMatches_WhenFiltering_ThenResultIsCapped()
    {
        var candidates = Enumerable.Range(0, 50).Select(index => SuggestionFilter.FromLabel($"label-{index}"));

        var result = SuggestionFilter.Filter(candidates, "label");

        Assert.Equal(SuggestionFilter.MaxResults, result.Count);
    }

    [Fact]
    public void GivenDuplicateIds_WhenFiltering_ThenFirstWins()
    {
        Suggestion[] candidates = [new("1", "Alpha", null, false), new("1", "Alpha two", null, true)];

        var result = SuggestionFilter.Filter(candidates, "alpha");

        Assert.False(Assert.Single(result).IsInactive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void GivenBlankQuery_WhenFiltering_ThenNothingMatches(string? query)
    {
        Assert.Empty(SuggestionFilter.Filter([SuggestionFilter.FromLabel("a")], query));
    }
}
