using Geeratya.Core.SavedViews;

namespace Geeratya.Core.Tests.SavedViews;

[Trait("Os", "Independent")]
public class SavedViewListTests
{
    private static readonly SavedView _first = new("Bugs", "Jql", "type = Bug", "List");
    private static readonly SavedView _second = new("Board", "Board", "4", "Columns");

    [Fact]
    public void GivenTheSameNameInOtherCase_WhenSaving_ThenTheViewIsReplacedInPlace()
    {
        var list = SavedViewList.Empty.Saved(_first).Saved(_second);

        var replaced = list.Saved(_first with { Name = "BUGS", SourceValue = "type = Epic" });

        Assert.Equal(["BUGS", "Board"], replaced.Items.Select(item => item.Name));
        Assert.Equal("type = Epic", replaced.Items[0].SourceValue);
    }

    [Fact]
    public void GivenTheCap_WhenSavingAnotherName_ThenTheListIsUnchanged()
    {
        var list = SavedViewList.Empty;

        for (var index = 0; index < SavedViewList.MaxViews; index++)
        {
            list = list.Saved(_first with { Name = $"v{index}" });
        }

        Assert.Same(list, list.Saved(_first with { Name = "one more" }));
    }

    [Fact]
    public void GivenANameTakenByAnotherView_WhenRenaming_ThenTheListIsUnchanged()
    {
        var list = SavedViewList.Empty.Saved(_first).Saved(_second);

        Assert.Same(list, list.Renamed("Bugs", "board"));
    }

    [Fact]
    public void GivenOnlyTheCaseChanges_WhenRenaming_ThenItIsAllowed()
    {
        var list = SavedViewList.Empty.Saved(_first).Renamed("Bugs", "bugs");

        Assert.Equal("bugs", Assert.Single(list.Items).Name);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  Mine ", "Mine")]
    public void GivenAName_WhenCleaning_ThenItIsTrimmedOrRefused(string name, string? expected)
    {
        Assert.Equal(expected, SavedViewList.CleanName(name));
    }

    [Fact]
    public void GivenATooLongName_WhenCleaning_ThenItIsRefused()
    {
        Assert.Null(SavedViewList.CleanName(new string('x', SavedViewList.MaxNameLength + 1)));
    }
}
