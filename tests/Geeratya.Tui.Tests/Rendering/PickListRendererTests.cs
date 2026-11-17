using Geeratya.Core.Lists;
using Geeratya.Core.Mouse;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Text;
using Geeratya.Jira.Suggestions.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class PickListRendererTests
{
    private static readonly SuggestionSource _source = new(SuggestionKind.Labels, "A-1", []);

    [Fact]
    public void GivenAHighlightedRow_WhenBuildingARow_ThenOnlyThatRowTakesTheSelectedRole()
    {
        Assert.Equal(TextRole.Selected, PickListRenderer.Row("one", true).Spans[0].Role);
        Assert.Equal(TextRole.Body, PickListRenderer.Row("one", false).Spans[0].Role);
        Assert.Equal("    one", PickListRenderer.Row("one", false, "  ").PlainText);
    }

    [Theory]
    [InlineData(PickStatus.Loading, 3, true, "searching…")]
    [InlineData(PickStatus.Failed, 3, true, "lookup failed, showing what is loaded: boom")]
    [InlineData(PickStatus.Ready, 0, false, "No match")]
    [InlineData(PickStatus.Ready, 0, true, "No more values")]
    public void GivenAStatus_WhenBuildingTheNotice_ThenItSaysWhatIsGoingOn(
        PickStatus status,
        int count,
        bool queryEmpty,
        string expected)
    {
        var notice = PickListRenderer.Notice(new PickList(0, status, "boom"), count, queryEmpty);

        Assert.Equal(expected, notice!.Text);
    }

    [Theory]
    [InlineData(PickStatus.Ready, 3)]
    [InlineData(PickStatus.Waiting, 0)]
    public void GivenRowsOrAQuietWait_WhenBuildingTheNotice_ThenThereIsNone(PickStatus status, int count)
    {
        Assert.Null(PickListRenderer.Notice(new PickList(0, status), count, false));
    }

    [Fact]
    public void GivenMoreRowsThanTheWindow_WhenBuildingAPopup_ThenFiveRowsShowAroundTheHighlightAndEachKnowsItsIndex()
    {
        var popup = Popup(12, cursor: 8);

        var rows = PickListRenderer.Popup(popup, 30);

        Assert.Equal(PickListRenderer.PopupRows, rows.Count);
        Assert.Contains(rows, row => row.Item == 8);
        Assert.Equal(TextRole.Selected, rows.Single(row => row.Item == 8).Line.Spans[0].Role);
        Assert.Equal(rows.Select(row => row.Item), Enumerable.Range(rows[0].Item, rows.Count));
    }

    [Fact]
    public void GivenAFailedLookup_WhenBuildingAPopup_ThenTheNoticeRowFollowsTheItemsAndIsNotClickable()
    {
        var popup = Popup(2, cursor: 0) with { Pick = new PickList(0, PickStatus.Failed, "boom") };

        var rows = PickListRenderer.Popup(popup, 60, "  ");

        Assert.Equal([0, 1, -1], rows.Select(row => row.Item));
        Assert.Contains("lookup failed", rows[^1].Line.PlainText, StringComparison.Ordinal);
        Assert.StartsWith("  ", rows[^1].Line.PlainText, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNoHighlight_WhenBuildingAPopup_ThenNoRowIsSelected()
    {
        var popup = Popup(3, cursor: -1);

        var rows = PickListRenderer.Popup(popup, 30);

        Assert.DoesNotContain(rows, row => row.Line.Spans[0].Role == TextRole.Selected);
    }

    [Fact]
    public void GivenAScrolledWindow_WhenBuildingWalls_ThenTheScrollMarkersShowOnTheEdgeRows()
    {
        var lines = Enumerable.Range(0, 20).Select(index => StyledLine.Of($"row {index}")).ToList();
        var window = PickList.WindowAt(10, 20, 5);

        var walls = PickListRenderer.Walls(lines, window, 5, 20);

        Assert.Equal(5, walls.Count);
        Assert.Contains("▲", walls[0].PlainText, StringComparison.Ordinal);
        Assert.Contains("▼", walls[^1].PlainText, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAWindow_WhenMarkingRegions_ThenEachShownRowIsRegisteredWithItsIndexOnItsBoxRow()
    {
        var regions = new List<OverlayRegion>();
        var window = new PickWindow(4, 3);

        PickListRenderer.Mark(regions, MouseTarget.JumpRow, new BoxPlace(2, 10, 40), 2, window);

        Assert.Equal([4, 5, 6], regions.Select(region => region.Index));
        Assert.Equal([4, 5, 6], regions.Select(region => region.Top));
        Assert.All(regions, region => Assert.Equal(11, region.Left));
    }

    [Fact]
    public void GivenHeaderLines_WhenMarkingRegions_ThenOnlyTheLinesWithAnItemAreClickable()
    {
        var regions = new List<OverlayRegion>();

        var place = new BoxPlace(0, 0, 30);

        PickListRenderer.Mark(regions, MouseTarget.PickerRow, place, 2, new PickWindow(1, 3), [-1, -1, 0, -1, 1]);

        var only = Assert.Single(regions);

        Assert.Equal(0, only.Index);
        Assert.Equal(3, only.Top);
    }

    private static SuggestionState Popup(int count, int cursor)
    {
        var items = Enumerable.Range(0, count).Select(index => new Suggestion($"id{index}", $"label {index}", null, false)).ToList();

        return new SuggestionState(0, _source, "x", [], items, cursor, PickStatus.Ready, 1, null);
    }
}
