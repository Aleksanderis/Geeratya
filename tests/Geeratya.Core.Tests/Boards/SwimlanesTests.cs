using Geeratya.Core.Boards;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Boards;

[Trait("Os", "Independent")]
public class SwimlanesTests
{
    [Fact]
    public void GivenAssignees_WhenBuildingLanes_ThenTheyFollowFirstAppearanceAndUnassignedIsLast()
    {
        var rows = new[] { Row("A-1", "u2", "Bo"), Row("A-2", null), Row("A-3", "u1", "Al"), Row("A-4", "u2", "Bo") };

        var lanes = Swimlanes.Lanes(SwimlaneKind.Assignee, rows);

        Assert.Equal(["Bo", "Al", Swimlanes.UnassignedName], lanes.Select(lane => lane.Name));
        Assert.Equal([2, 1, 1], lanes.Select(lane => lane.Count));
    }

    [Fact]
    public void GivenEpics_WhenBuildingLanes_ThenIssuesWithoutAnEpicFormTheLastLane()
    {
        var rows = new[] { Row("A-1", null, epic: "E-1"), Row("A-2", null), Row("A-3", null, epic: "E-2") };

        var lanes = Swimlanes.Lanes(SwimlaneKind.Epic, rows);

        Assert.Equal(["E-1 Epic E-1", "E-2 Epic E-2", Swimlanes.NoEpicName], lanes.Select(lane => lane.Name));
    }

    [Fact]
    public void GivenNoGroupingOrNoRows_WhenBuildingLanes_ThenThereAreNone()
    {
        Assert.Empty(Swimlanes.Lanes(SwimlaneKind.None, [Row("A-1", "u1", "Al")]));
        Assert.Empty(Swimlanes.Lanes(SwimlaneKind.Assignee, []));
    }

    [Fact]
    public void GivenALaneKey_WhenFiltering_ThenOnlyThatLaneRemainsAndAVanishedKeyShowsTheFirstLane()
    {
        var rows = new[] { Row("A-1", "u1", "Al"), Row("A-2", "u2", "Bo"), Row("A-3", "u2", "Bo") };

        var second = Swimlanes.Filter(SwimlaneKind.Assignee, "u2", rows);
        var vanished = Swimlanes.Filter(SwimlaneKind.Assignee, "gone", rows);
        var none = Swimlanes.Filter(SwimlaneKind.None, "u2", rows);

        Assert.Equal(["A-2", "A-3"], second.Select(row => row.Key));
        Assert.Equal(["A-1"], vanished.Select(row => row.Key));
        Assert.Equal(3, none.Count);
    }

    [Fact]
    public void GivenLanes_WhenStepping_ThenItWrapsInBothDirections()
    {
        var rows = new[] { Row("A-1", "u1", "Al"), Row("A-2", "u2", "Bo") };
        var lanes = Swimlanes.Lanes(SwimlaneKind.Assignee, rows);

        Assert.Equal("u2", Swimlanes.Step(lanes, "u1", 1)?.Key);
        Assert.Equal("u1", Swimlanes.Step(lanes, "u2", 1)?.Key);
        Assert.Equal("u2", Swimlanes.Step(lanes, "u1", -1)?.Key);
        Assert.Null(Swimlanes.Step([], null, 1));
    }

    [Fact]
    public void GivenTheGroupings_WhenCycling_ThenTheyGoNoneAssigneeEpicLabelsNone()
    {
        Assert.Equal(SwimlaneKind.Assignee, Swimlanes.Next(SwimlaneKind.None));
        Assert.Equal(SwimlaneKind.Epic, Swimlanes.Next(SwimlaneKind.Assignee));
        Assert.Equal(SwimlaneKind.Labels, Swimlanes.Next(SwimlaneKind.Epic));
        Assert.Equal(SwimlaneKind.None, Swimlanes.Next(SwimlaneKind.Labels));
    }

    [Fact]
    public void GivenCardsWithSeveralLabels_WhenGroupingByLabels_ThenACardIsInEachOfItsLanesAndUnlabelledLast()
    {
        var rows = new[]
        {
            Row("A-1", null, labels: ["ui", "api"]),
            Row("A-2", null),
            Row("A-3", null, labels: ["api", "api"]),
            Row("A-5", null, labels: [" "]),
        };

        var lanes = Swimlanes.Lanes(SwimlaneKind.Labels, rows);

        Assert.Equal(["ui", "api", ""], lanes.Select(lane => lane.Key));
        Assert.Equal([1, 2, 2], lanes.Select(lane => lane.Count));
        Assert.Equal("No label", lanes[2].Name);
        Assert.Equal(["A-1", "A-3"], Swimlanes.Filter(SwimlaneKind.Labels, "api", rows).Select(row => row.Key));
        Assert.Equal(["A-1"], Swimlanes.Filter(SwimlaneKind.Labels, "ui", rows).Select(row => row.Key));
        Assert.Equal(["A-2", "A-5"], Swimlanes.Filter(SwimlaneKind.Labels, "", rows).Select(row => row.Key));
    }

    private static IssueSummary Row(
        string key,
        string? accountId,
        string name = "",
        string? epic = null,
        string[]? labels = null)
    {
        return new IssueSummary(
            key,
            key,
            $"Summary {key}",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            accountId is null ? null : new IssueAssignee(accountId, name),
            labels ?? [],
            epic is null ? null : new IssueParent(epic, $"Epic {epic}", true),
            null,
            "A");
    }
}
