using Geeratya.Core.Text;

namespace Geeratya.Core.Tests.Text;

[Trait("Os", "Independent")]
public class PromptWindowTests
{
    [Fact]
    public void GivenTheCaretAtTheEnd_WhenTheTextFits_ThenTheMarkIsAnUnderscoreAfterIt()
    {
        var window = PromptWindow.Of(PromptText.Of("abc"), 20);

        Assert.Equal(new PromptWindow("abc", "_", string.Empty), window);
    }

    [Fact]
    public void GivenTheCaretInsideTheText_WhenTheTextFits_ThenTheMarkIsABarBetweenTwoCharacters()
    {
        var window = PromptWindow.Of(new PromptText("abc", 1), 20);

        Assert.Equal(new PromptWindow("ab", "|", "c"), window);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(20)]
    [InlineData(39)]
    public void GivenALongText_WhenCut_ThenTheCaretStaysInViewAndTheWidthIsKept(int back)
    {
        var prompt = new PromptText(new string('a', 20) + new string('b', 20), back);

        var window = PromptWindow.Of(prompt, 12);

        var shown = $"{window.Before}{window.Caret}{window.After}";
        Assert.True(DisplayWidth.Of(shown) <= 12, shown);
        Assert.Equal(prompt.Caret == 40 ? "_" : "|", window.Caret);
    }

    [Fact]
    public void GivenACaretNearTheStartOfALongText_WhenCut_ThenTheTailIsCutWithAnEllipsis()
    {
        var window = PromptWindow.Of(new PromptText("abcdefghijklmnopqrstuvwxyz", 24), 10);

        Assert.StartsWith("ab", window.Before, StringComparison.Ordinal);
        Assert.EndsWith("…", window.After, StringComparison.Ordinal);
        Assert.DoesNotContain("…", window.Before, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheCaretAtTheEndOfALongText_WhenCut_ThenTheTailShowsAfterALeadingEllipsis()
    {
        var window = PromptWindow.Of(PromptText.Of("abcdefghijklmnopqrstuvwxyz"), 10);

        Assert.Equal("…stuvwxyz", window.Before);
        Assert.Equal(string.Empty, window.After);
    }

    [Fact]
    public void GivenWideCharacters_WhenCut_ThenNoCharacterIsSplitAndTheWidthFits()
    {
        var prompt = new PromptText("日本語日本語日本語", 4);

        var window = PromptWindow.Of(prompt, 9);

        var shown = $"{window.Before}{window.Caret}{window.After}";
        Assert.True(DisplayWidth.Of(shown) <= 9, shown);
        Assert.DoesNotContain("�", shown, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenATinyWidth_WhenCut_ThenAtLeastTheMarkShows()
    {
        var window = PromptWindow.Of(PromptText.Of("abcdef"), 1);

        Assert.Equal("_", window.Caret);
        Assert.Equal(string.Empty, window.After);
    }
}
