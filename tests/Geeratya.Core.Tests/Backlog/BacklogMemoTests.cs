using Geeratya.Core.Backlog;
using Geeratya.Core.Lists;
using Geeratya.Core.Selection;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Backlog;

[Trait("Os", "Independent")]
public class BacklogMemoTests
{
    private static readonly IReadOnlyList<IssueSummary> _rows = BacklogSections.Order(BacklogFixtures.RankedRows);

    [Fact]
    public void GivenALayout_WhenOnlyTheCursorMoves_ThenTheSameLayoutComesBack()
    {
        var tab = BacklogFixtures.Tab(_rows);
        var layout = BacklogSections.Build(tab);

        var moved = tab with { Selected = 3, SelectedKey = _rows[3].Key, Scroll = 2 };

        Assert.Same(layout, BacklogSections.Build(moved));
    }

    [Fact]
    public void GivenALayout_WhenTheRowsChange_ThenTheLayoutIsRebuilt()
    {
        var tab = BacklogFixtures.Tab(_rows);
        var layout = BacklogSections.Build(tab);

        var changed = tab with { Rows = [.. _rows.Skip(1)] };

        Assert.NotSame(layout, BacklogSections.Build(changed));
        Assert.Equal(_rows.Count - 1, BacklogSections.Build(changed).Sections.Sum(section => section.RowCount));
    }

    [Fact]
    public void GivenALayout_WhenASectionCollapsesOrTheSprintsChange_ThenTheLayoutIsRebuilt()
    {
        var tab = BacklogFixtures.Tab(_rows);
        var layout = BacklogSections.Build(tab);

        var collapsed = tab with { CollapsedSections = [BacklogSections.BacklogKey] };
        var withEmpty = tab with { Sprints = [BacklogFixtures.EmptyFuture] };

        Assert.NotSame(layout, BacklogSections.Build(collapsed));
        Assert.True(BacklogSections.Build(collapsed).Sections.Single(section => section.Key == "backlog").Collapsed);
        Assert.Equal(layout.Sections.Count + 1, BacklogSections.Build(withEmpty).Sections.Count);
    }

    [Fact]
    public void GivenRowsWithAnEstimate_WhenAskingForColumns_ThenThePointsColumnFollowsTheRows()
    {
        var tab = BacklogFixtures.Tab(_rows);
        var bare = tab with { Rows = [.. _rows.Select(row => row with { Estimate = null })] };

        Assert.True(ListViewColumns.ForBacklog(tab, 120).Points > 0);
        Assert.Equal(0, ListViewColumns.ForBacklog(bare, 120).Points);
        Assert.True(ListViewColumns.ForBacklog(tab, 120).Points > 0);
    }

    [Fact]
    public void GivenVisualMode_WhenTheRowsReorder_ThenTheRangeFollowsTheAnchorKey()
    {
        var tab = BacklogFixtures.Tab(_rows) with { VisualAnchor = "A-3", Selected = 5 };
        var anchorRow = _rows.ToList().FindIndex(row => row.Key == "A-3");

        Assert.Equal(new RowRange(anchorRow, 5), RowSelection.VisualRange(tab));

        var reordered = tab with { Rows = [.. _rows.Reverse()] };
        var after = _rows.Count - 1 - anchorRow;

        Assert.Equal(new RowRange(Math.Min(after, 5), Math.Max(after, 5)), RowSelection.VisualRange(reordered));
    }

    [Fact]
    public void GivenACollapsedSectionInTheRange_WhenCounting_ThenItsHiddenRowsAreLeftOut()
    {
        var tab = BacklogFixtures.Tab(_rows) with
        {
            VisualAnchor = _rows[0].Key,
            Selected = _rows.Count - 1,
            CollapsedSections = [BacklogSections.BacklogKey],
        };

        Assert.Equal(RowSelection.Targets(tab).Count, RowSelection.Count(tab));
        Assert.True(RowSelection.Count(tab) < _rows.Count);
    }

    [Fact]
    public void GivenDuplicateAndMixedCaseKeys_WhenReadingTheRange_ThenTheAnchorIsTheFirstMatchingRow()
    {
        var rows = new[] { _rows[0], _rows[1] with { Key = _rows[0].Key }, _rows[2], _rows[3] };
        var tab = BacklogFixtures.Tab(rows) with { VisualAnchor = _rows[0].Key.ToLowerInvariant(), Selected = 3 };

        Assert.Equal(new RowRange(0, 3), RowSelection.VisualRange(tab));
    }

    [Fact]
    public void GivenAnAnchorThatIsGone_WhenReadingTheRange_ThenItFallsBackToTheCursorRow()
    {
        var tab = BacklogFixtures.Tab(_rows) with { VisualAnchor = "Z-99", Selected = 2 };

        Assert.Equal(new RowRange(2, 2), RowSelection.VisualRange(tab));
        Assert.Equal(1, RowSelection.Count(tab));
    }

    [Fact]
    public void GivenNoCollapsedSection_WhenCounting_ThenTheCountIsTheRangeLength()
    {
        var tab = BacklogFixtures.Tab(_rows) with { VisualAnchor = _rows[1].Key, Selected = 4 };

        Assert.Equal(4, RowSelection.Count(tab));
    }
}
