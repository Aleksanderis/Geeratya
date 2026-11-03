using Geeratya.Core.Backlog;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Tests.Backlog;

[Trait("Os", "Independent")]
public class BacklogSectionsTests
{
    private static readonly Sprint[] _sprints =
        [BacklogFixtures.Active, BacklogFixtures.Future, BacklogFixtures.EmptyFuture];

    [Fact]
    public void GivenRankedRows_WhenOrdered_ThenActiveThenFutureThenBacklogWithRankKeptInside()
    {
        var ordered = BacklogSections.Order(BacklogFixtures.RankedRows);

        Assert.Equal(["A-3", "A-4", "A-2", "A-6", "A-1", "A-5"], ordered.Select(row => row.Key));
    }

    [Fact]
    public void GivenRowsAlreadyInSectionOrder_WhenOrdered_ThenTheSameListComesBack()
    {
        var ordered = BacklogSections.Order(BacklogFixtures.RankedRows);

        Assert.Same(ordered, BacklogSections.Order(ordered));
    }

    [Fact]
    public void GivenAClosedSprintOnlyIssue_WhenGrouped_ThenItBelongsToTheBacklog()
    {
        var closed = new Sprint(1, "Old", SprintState.Closed, null, null, null, null, 6);
        var row = BacklogFixtures.Issue("A-9", closed, 1);

        Assert.Equal(BacklogSections.BacklogKey, BacklogSections.KeyOf(row));
    }

    [Fact]
    public void GivenSprintsAndRows_WhenLaidOut_ThenEachSectionCountsItsRowsAndPoints()
    {
        var tab = BacklogFixtures.Tab(BacklogSections.Order(BacklogFixtures.RankedRows), _sprints);

        var layout = BacklogSections.Build(tab);

        Assert.Equal(["Sprint 1", "Sprint 2", "Sprint 3", "Backlog"], layout.Sections.Select(section => section.Name));
        Assert.Equal([2, 2, 0, 2], layout.Sections.Select(section => section.RowCount));
        Assert.Equal([5.0, 3.0, 0.0, 3.5], layout.Sections.Select(section => section.Points));
        Assert.Equal([true, true, false, true], layout.Sections.Select(section => section.HasEstimates));
        Assert.Equal(12, layout.LineCount);
    }

    [Fact]
    public void GivenAnEmptyFutureSprint_WhenLaidOut_ThenItShowsBetweenItsNeighboursWithoutRows()
    {
        var tab = BacklogFixtures.Tab(BacklogSections.Order(BacklogFixtures.RankedRows), _sprints);

        var empty = BacklogSections.Build(tab).Sections[2];

        Assert.Equal(0, empty.RowCount);
        Assert.Equal(4, empty.FirstRow);
        Assert.False(empty.HasRows);
        Assert.Equal(2, empty.LineCount);
    }

    [Fact]
    public void GivenNoSprintListYet_WhenLaidOut_ThenSectionsTakeTheirNamesFromTheIssues()
    {
        var tab = BacklogFixtures.Tab(BacklogSections.Order(BacklogFixtures.RankedRows));

        var layout = BacklogSections.Build(tab);

        Assert.Equal(["Sprint 1", "Sprint 2", "Backlog"], layout.Sections.Select(section => section.Name));
        Assert.All(layout.Sections, section => Assert.Null(section.Sprint));
        Assert.Equal(SprintState.Active, layout.Sections[0].State);
    }

    [Fact]
    public void GivenOnlyTheBacklog_WhenLaidOut_ThenThereIsOneSectionWithoutASprint()
    {
        var rows = new[] { BacklogFixtures.Issue("A-1", null, null), BacklogFixtures.Issue("A-2", null, null) };

        var layout = BacklogSections.Build(BacklogFixtures.Tab(rows));

        var only = Assert.Single(layout.Sections);
        Assert.True(only.IsBacklog);
        Assert.False(only.HasEstimates);
    }

    [Fact]
    public void GivenNoRowsAndNoSprints_WhenLaidOut_ThenThereAreNoSectionsAndNoLines()
    {
        var layout = BacklogSections.Build(BacklogFixtures.Tab([]));

        Assert.Empty(layout.Sections);
        Assert.Equal(0, layout.LineCount);
        Assert.Empty(layout.Stops);
    }

    [Fact]
    public void GivenACollapsedSection_WhenLaidOut_ThenOnlyItsHeaderTakesALineAndItsFirstRowIsTheStop()
    {
        var tab = BacklogFixtures.Tab(BacklogSections.Order(BacklogFixtures.RankedRows), _sprints) with
        {
            CollapsedSections = [BacklogSections.SprintKey(BacklogFixtures.ActiveId)],
        };

        var layout = BacklogSections.Build(tab);

        Assert.Equal(1, layout.Sections[0].LineCount);
        Assert.Equal(9, layout.LineCount);
        Assert.Equal([0, 2, 3, 4, 5], layout.Stops);
        Assert.Equal(0, layout.StopFor(1));
        Assert.Equal(0, layout.LineOfRow(1));
    }

    [Fact]
    public void GivenLines_WhenLookedUp_ThenHeadersGoalsRowsAndNotesAreTold()
    {
        var tab = BacklogFixtures.Tab(BacklogSections.Order(BacklogFixtures.RankedRows), _sprints);
        var layout = BacklogSections.Build(tab);

        Assert.Null(layout.LineAt(0)!.Value.Row);
        Assert.Null(layout.LineAt(1)!.Value.Row);
        Assert.Equal(0, layout.LineAt(2)!.Value.Row);
        Assert.Null(layout.LineAt(8)!.Value.Row);
        Assert.Equal(5, layout.LineAt(11)!.Value.Row);
        Assert.Null(layout.LineAt(12));
    }
}
