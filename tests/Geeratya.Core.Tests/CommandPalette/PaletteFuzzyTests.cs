using Geeratya.Core.CommandPalette;

namespace Geeratya.Core.Tests.CommandPalette;

[Trait("Os", "Independent")]
public class PaletteFuzzyTests
{
    [Fact]
    public void GivenAnEmptyFilter_WhenScoring_ThenEverythingMatchesEqually()
    {
        Assert.Equal(0, PaletteFuzzy.Score(string.Empty, "Quit", "app.quit"));
    }

    [Fact]
    public void GivenSubstringTitleIdAndLetters_WhenScoring_ThenTheOrderIsTitleIdLetters()
    {
        var title = PaletteFuzzy.Score("edit", "Edit the issue", "issue.edit");
        var id = PaletteFuzzy.Score("edit", "Change fields", "issue.edit");
        var letters = PaletteFuzzy.Score("edt", "Edit the issue", "issue.change");
        var idLetters = PaletteFuzzy.Score("ised", "Change fields", "issue.edit");

        Assert.True(title > id);
        Assert.True(id > letters);
        Assert.True(letters > idLetters);
        Assert.NotNull(idLetters);
    }

    [Fact]
    public void GivenALaterMatch_WhenScoring_ThenItRanksBelowAnEarlierOne()
    {
        var early = PaletteFuzzy.Score("link", "Link the issue", "issue.link");
        var late = PaletteFuzzy.Score("link", "Remove the link", "issue.unlink");

        Assert.True(early > late);
    }

    [Fact]
    public void GivenLettersOutOfOrder_WhenScoring_ThenThereIsNoMatch()
    {
        Assert.Null(PaletteFuzzy.Score("tiuq", "Quit", "app.quit"));
    }

    [Fact]
    public void GivenOtherCase_WhenScoring_ThenItStillMatches()
    {
        Assert.NotNull(PaletteFuzzy.Score("QUIT", "Quit", "app.quit"));
        Assert.NotNull(PaletteFuzzy.Score("qt", "Quit", "app.quit"));
    }
}
