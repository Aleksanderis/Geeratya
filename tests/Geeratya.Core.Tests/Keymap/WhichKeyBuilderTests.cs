using Geeratya.Core.Keymap;

namespace Geeratya.Core.Tests.Keymap;

[Trait("Os", "Independent")]
public class WhichKeyBuilderTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Fact]
    public void GivenSpaceI_WhenBuildingRows_ThenIssueActionsFormOneClusterInAlphabeticalOrder()
    {
        var labels = WhichKeyBuilder.Build(_keymap, InputMode.Normal, "Space i").Select(row => row.Label).ToList();

        Assert.DoesNotContain(string.Empty, labels);
        Assert.Equal(labels.OrderBy(label => label, StringComparer.OrdinalIgnoreCase), labels);
    }

    [Fact]
    public void GivenSpace_WhenBuildingRows_ThenPlainClustersComeBeforeGroupsAndLoneActionsAreLast()
    {
        var rows = WhichKeyBuilder.Build(_keymap, InputMode.Normal, "Space");
        var firstGroup = rows.ToList().FindIndex(row => row.Label.StartsWith('+'));
        var lastHeader = rows.ToList().FindLastIndex(row => row.IsHeader);

        Assert.True(firstGroup > 0);
        Assert.True(rows.Take(firstGroup).All(row => !row.Label.StartsWith('+')));
        Assert.Equal("More", rows[firstGroup - 1].Label);
        Assert.True(rows[firstGroup - 1].IsHeader);
        Assert.True(lastHeader < firstGroup);
        var issueCount = _keymap.NextKeys(InputMode.Normal, "Space").First(next => next.Key == "i").Count;

        Assert.Contains(rows, row => row.Label == $"+issue ({issueCount})");
        Assert.Contains(rows, row => row.Label.StartsWith("+comment (", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.Label.StartsWith("+rank (", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenSpace_WhenBuildingRows_ThenTheSubtaskGroupIsNamedSubTaskNotIssue()
    {
        var rows = WhichKeyBuilder.Build(_keymap, InputMode.Normal, "Space");
        var subtaskCount = _keymap.NextKeys(InputMode.Normal, "Space").First(next => next.Key == "t").Count;

        Assert.Contains(rows, row => row.Key == "t" && row.Label == $"+sub-task ({subtaskCount})");
        Assert.Single(rows, row => row.Label.StartsWith("+issue (", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAGroupWithMixedActionIds_WhenBuildingRows_ThenItsNounIsTheLastSharedWord()
    {
        var keymap = new KeymapTable(
        [
            new KeyBinding(InputMode.Normal, "Space g a", "issue.link-add"),
            new KeyBinding(InputMode.Normal, "Space g d", "issue.link-delete"),
            new KeyBinding(InputMode.Normal, "Space h a", "issue.assign"),
            new KeyBinding(InputMode.Normal, "Space h c", "comment.add"),
        ]);

        var labels = WhichKeyBuilder.Build(keymap, InputMode.Normal, "Space").Select(row => row.Label).ToList();

        Assert.Contains("+link (2)", labels);
        Assert.Contains("+issue (2)", labels);
    }

    [Fact]
    public void GivenAnActionListWithSharedPrefixes_WhenBuilding_ThenEachClusterHasAHeaderAndABlankRowBeforeIt()
    {
        var keymap = new KeymapTable(
        [
            new KeyBinding(InputMode.Normal, "Space b", "rank.top"),
            new KeyBinding(InputMode.Normal, "Space a", "rank.bottom"),
            new KeyBinding(InputMode.Normal, "Space c", "issue.delete"),
            new KeyBinding(InputMode.Normal, "Space d", "issue.create"),
            new KeyBinding(InputMode.Normal, "Space z", "tab.new"),
        ]);
        var rows = WhichKeyBuilder.Build(keymap, InputMode.Normal, "Space");

        Assert.Equal(["", "d", "c", "", "", "a", "b", "", "", "z"], rows.Select(row => row.Key));
        Assert.Equal(["Issue", "Rank", "Other"], rows.Where(row => row.IsHeader).Select(row => row.Label));
    }

    [Fact]
    public void GivenOnlyOnePart_WhenBuilding_ThenNoHeaderShows()
    {
        var keymap = new KeymapTable(
        [
            new KeyBinding(InputMode.Normal, "Space b", "rank.top"),
            new KeyBinding(InputMode.Normal, "Space a", "rank.bottom"),
        ]);

        Assert.DoesNotContain(WhichKeyBuilder.Build(keymap, InputMode.Normal, "Space"), row => row.IsHeader);
    }

    [Fact]
    public void GivenEverydayLevel_WhenBuildingSpace_ThenAGroupOfOnlyAdvancedActionsDisappears()
    {
        var everyday = WhichKeyBuilder.Build(_keymap, InputMode.Normal, "Space", CommandLevel.Everyday);
        var advanced = WhichKeyBuilder.Build(_keymap, InputMode.Normal, "Space");

        Assert.DoesNotContain(everyday, row => row.Label.StartsWith("+rank (", StringComparison.Ordinal));
        Assert.Contains(advanced, row => row.Label.StartsWith("+rank (", StringComparison.Ordinal));
        Assert.Contains(everyday, row => row.Label.StartsWith("+issue (", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenEverydayLevel_WhenBuildingSpaceI_ThenAdvancedActionsAreLeftOut()
    {
        var everyday = WhichKeyBuilder.Build(_keymap, InputMode.Normal, "Space i", CommandLevel.Everyday);
        var advanced = WhichKeyBuilder.Build(_keymap, InputMode.Normal, "Space i");
        var deleteTitle = ActionCatalog.FindTitle(ActionIds.IssueDelete);

        Assert.DoesNotContain(everyday, row => row.Label == deleteTitle);
        Assert.Contains(advanced, row => row.Label == deleteTitle);
    }

    [Fact]
    public void GivenEverydayLevel_WhenAskingForNextKeysUnderAnAllAdvancedGroup_ThenTheListIsEmpty()
    {
        Assert.Empty(_keymap.NextKeys(InputMode.Normal, "Space k", CommandLevel.Everyday));
        Assert.NotEmpty(_keymap.NextKeys(InputMode.Normal, "Space k"));
    }
}
