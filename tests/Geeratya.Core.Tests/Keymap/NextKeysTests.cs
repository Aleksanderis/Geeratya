using Geeratya.Core.Keymap;

namespace Geeratya.Core.Tests.Keymap;

[Trait("Os", "Independent")]
public class NextKeysTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Fact]
    public void GivenEveryMultiKeyBindingOfEveryMode_WhenAskingForTheNextKeys_ThenTheKeymapRowIsListed()
    {
        var checkedRows = 0;

        foreach (var binding in _keymap.Bindings.Where(binding => binding.Keys.Contains(' ')))
        {
            var parts = binding.Keys.Split(' ');
            var pending = string.Join(' ', parts[..^1]);
            var next = _keymap.NextKeys(binding.Mode, pending).Single(entry => entry.Key == parts[^1]);

            Assert.Equal(binding.Action, next.Action);
            checkedRows++;
        }

        Assert.True(checkedRows > 0);
    }

    [Fact]
    public void GivenTheSpaceLeader_WhenAskingForTheNextKeys_ThenGroupsCarryTheCountOfTheirActions()
    {
        var next = _keymap.NextKeys(InputMode.Normal, "Space");
        var issue = Assert.Single(next, entry => entry.Key == "i");
        var issueRows = _keymap.Bindings.Count(binding => binding.Keys.StartsWith("Space i ", StringComparison.Ordinal));

        Assert.Null(issue.Action);
        Assert.Equal(issueRows, issue.Count);
        Assert.Equal(ActionIds.TabNew, Assert.Single(next, entry => entry.Key == "t").Action);
    }

    [Fact]
    public void GivenAKeyThatContinuesNothing_WhenAskingForTheNextKeys_ThenTheListIsEmpty()
    {
        Assert.Empty(_keymap.NextKeys(InputMode.Normal, "j"));
        Assert.Empty(_keymap.NextKeys(InputMode.Help, "Space"));
    }
}
