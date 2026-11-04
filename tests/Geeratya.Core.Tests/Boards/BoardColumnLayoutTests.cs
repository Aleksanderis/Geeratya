using Geeratya.Core.Boards;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Boards;

[Trait("Os", "Independent")]
public class BoardColumnLayoutTests
{
    private const int Cell = 40;

    [Fact]
    public void GivenCardsOfDifferentHeight_WhenPlacing_ThenEachStartsWhereThePreviousEnded()
    {
        var group = Group(Issue("A-1", "Short", []), Issue("A-2", "Short", ["x"]), Issue("A-3", "Short", []));

        var placed = BoardColumnLayout.Place(group, 0, Cell, 40);

        Assert.Equal([0, 5, 12], placed.Select(static card => card.Top));
        Assert.Equal([5, 7, 5], placed.Select(static card => card.Rows.Count));
    }

    [Fact]
    public void GivenNotEnoughRowsForTheNextCard_WhenPlacing_ThenOnlyWholeCardsArePlaced()
    {
        var group = Group(Issue("A-1", "Short", []), Issue("A-2", "Short", ["x"]));

        var placed = BoardColumnLayout.Place(group, 0, Cell, 11);

        Assert.Equal([0], placed.Select(static card => card.Card));
    }

    [Fact]
    public void GivenAFirstCard_WhenPlacing_ThenTheListStartsAtThatCard()
    {
        var group = Group(Issue("A-1", "Short", []), Issue("A-2", "Short", []), Issue("A-3", "Short", []));

        var placed = BoardColumnLayout.Place(group, 1, Cell, 10);

        Assert.Equal([1, 2], placed.Select(static card => card.Card));
        Assert.Equal(0, placed[0].Top);
    }

    [Fact]
    public void GivenARowInsideACard_WhenLookingItUp_ThenTheCardAndNoneBelowTheLastOneAreFound()
    {
        var group = Group(Issue("A-1", "Short", []), Issue("A-2", "Short", []));
        var placed = BoardColumnLayout.Place(group, 0, Cell, 20);

        Assert.Equal(0, BoardColumnLayout.At(placed, 4)?.Card);
        Assert.Equal(1, BoardColumnLayout.At(placed, 5)?.Card);
        Assert.Null(BoardColumnLayout.At(placed, 10));
    }

    [Fact]
    public void GivenAParentWithChildren_WhenPlacing_ThenThePartsFollowTheFrame()
    {
        var group = new BoardColumnGroup(
            "To Do",
            [Issue("A-1", "P", []), Issue("A-2", "C", []), Issue("A-3", "C", [])],
            [false, true, true]);

        var placed = BoardColumnLayout.Place(group, 0, Cell, 30);

        Assert.Equal([CardGroupPart.First, CardGroupPart.Middle, CardGroupPart.Last], placed.Select(static card => card.Part));
        Assert.Equal(CardRowKind.Divider, placed[2].Rows[^1].Kind);
    }

    private static BoardColumnGroup Group(params IssueSummary[] issues)
    {
        return new BoardColumnGroup("To Do", issues);
    }

    private static IssueSummary Issue(string key, string summary, IReadOnlyList<string> labels)
    {
        return new IssueSummary(
            key,
            key,
            summary,
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            labels,
            null,
            null,
            "A");
    }
}
