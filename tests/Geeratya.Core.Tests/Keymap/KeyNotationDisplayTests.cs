using Geeratya.Core.Keymap;

namespace Geeratya.Core.Tests.Keymap;

[Trait("Os", "Independent")]
public class KeyNotationDisplayTests
{
    [Theory]
    [InlineData("Space g", "<space>g")]
    [InlineData("Space i s", "<space>is")]
    [InlineData("Space", "<space>")]
    [InlineData("g g", "gg")]
    [InlineData("g t", "gt")]
    [InlineData("] ]", "]]")]
    [InlineData("Esc", "Esc")]
    [InlineData("Enter", "Enter")]
    [InlineData("Down", "Down")]
    [InlineData("Ctrl-w c", "Ctrl-w c")]
    [InlineData("Ctrl-d", "Ctrl-d")]
    [InlineData("Alt-1", "Alt-1")]
    [InlineData("Space Tab", "<space><Tab>")]
    [InlineData("g Esc", "g<Esc>")]
    [InlineData("Space Ctrl-w", "<space> Ctrl-w")]
    public void GivenStoredKeys_WhenDisplaying_ThenTheyReadLikeGitunia(string stored, string expected)
    {
        Assert.Equal(expected, KeyNotation.Display(stored));
    }

    [Fact]
    public void GivenTheDefaultKeymap_WhenDisplayingEveryBinding_ThenNoLeaderIsSpelledAsAWord()
    {
        foreach (var binding in DefaultKeymap.Load().Bindings)
        {
            var shown = KeyNotation.Display(binding.Keys);

            Assert.DoesNotContain("Space", shown);
            Assert.Equal(binding.Keys.Contains("Space"), shown.Contains("<space>"));
        }
    }

    [Fact]
    public void GivenNoKeys_WhenDisplaying_ThenNullStaysNull()
    {
        Assert.Null(KeyNotation.Display(null));
    }
}
