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

    [Fact]
    public void GivenEveryDefaultBinding_WhenDisplayedAndParsedBack_ThenTheStoredKeysReturn()
    {
        foreach (var binding in DefaultKeymap.Load().Bindings)
        {
            var parsed = ParseDisplay(KeyNotation.Display(binding.Keys));

            Assert.Equal(binding.Keys, parsed);
        }
    }

    [Fact]
    public void GivenABoundAction_WhenAskingForItsLabel_ThenItIsTheDisplayedKeys()
    {
        var keymap = new KeymapTable([new KeyBinding(InputMode.Normal, "Space g", ActionIds.IssueJump)]);

        Assert.Equal("<space>g", keymap.FindKeyLabel(InputMode.Normal, ActionIds.IssueJump));
        Assert.Null(keymap.FindKeyLabel(InputMode.Normal, ActionIds.IssueOpenInBrowser));
    }

    // The inverse of Display, for the round-trip test only: bracketed names, modifier keys up to the next space,
    // anything else one character at a time.
    private static string ParseDisplay(string shown)
    {
        var keys = new List<string>();
        var index = 0;

        while (index < shown.Length)
        {
            if (shown[index] == ' ')
            {
                index++;

                continue;
            }

            var rest = shown[index..];

            if (rest.Length > 2 && rest[0] == '<' && rest.IndexOf('>') is var close and > 1)
            {
                var name = rest[1..close];

                keys.Add(name == "space" ? "Space" : name);
                index += close + 1;

                continue;
            }

            if (rest.StartsWith("Ctrl-", StringComparison.Ordinal) || rest.StartsWith("Alt-", StringComparison.Ordinal))
            {
                var end = rest.IndexOf(' ');
                var length = end < 0 ? rest.Length : end;

                keys.Add(rest[..length]);
                index += length;

                continue;
            }

            var word = new string([.. rest.TakeWhile(character => character != ' ')]);

            if (word.Length > 1 && !word.Contains('<') && KeyNotation.IsValid(word))
            {
                keys.Add(word);
                index += word.Length;

                continue;
            }

            keys.Add(rest[..1]);
            index++;
        }

        return string.Join(' ', keys);
    }
}
