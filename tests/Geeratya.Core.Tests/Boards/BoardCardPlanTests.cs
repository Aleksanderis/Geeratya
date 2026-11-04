using Geeratya.Core.Boards;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Boards;

[Trait("Os", "Independent")]
public class BoardCardPlanTests
{
    private const int Cell = 40;

    [Fact]
    public void GivenAShortSummaryWithoutLabels_WhenPlanning_ThenTheCardHasASingleLineAndASpacingRow()
    {
        var rows = BoardCardPlan.Rows(Issue("Short", []), CardGroupPart.None, Cell);

        Assert.Equal(
            [CardRowKind.Top, CardRowKind.Summary, CardRowKind.Blank, CardRowKind.Key, CardRowKind.Bottom],
            rows.Select(static row => row.Kind));
    }

    [Fact]
    public void GivenALongSummaryAndLabels_WhenPlanning_ThenTheSummaryWrapsAndLabelsGetTheirOwnRowsWithSpacing()
    {
        var summary = "A summary that is much too long to fit on a single line of this card";
        var rows = BoardCardPlan.Rows(Issue(summary, ["alpha"]), CardGroupPart.None, Cell);

        Assert.Equal(
        [
            CardRowKind.Top,
            CardRowKind.Summary,
            CardRowKind.Summary,
            CardRowKind.Blank,
            CardRowKind.Labels,
            CardRowKind.Blank,
            CardRowKind.Key,
            CardRowKind.Bottom,
        ],
            rows.Select(static row => row.Kind));
    }

    [Fact]
    public void GivenManyLabelsOnALongSummary_WhenPlanning_ThenTheCardIsAtMostMaxRowsTall()
    {
        var labels = Enumerable.Range(0, 12).Select(static number => $"label-number-{number}").ToList();
        var summary = new string('x', 200);

        var rows = BoardCardPlan.Rows(Issue(summary, labels), CardGroupPart.None, Cell);

        Assert.Equal(BoardCardPlan.MaxRows, rows.Count);
    }

    [Theory]
    [InlineData(CardGroupPart.First, CardRowKind.Top, CardRowKind.Divider)]
    [InlineData(CardGroupPart.Middle, CardRowKind.Summary, CardRowKind.Divider)]
    [InlineData(CardGroupPart.Last, CardRowKind.Summary, CardRowKind.Divider)]
    [InlineData(CardGroupPart.None, CardRowKind.Top, CardRowKind.Bottom)]
    public void GivenAGroupPart_WhenPlanning_ThenOnlyTheParentAndSingleCardsHaveATopAndMembersEndWithADivider(
        CardGroupPart part,
        CardRowKind first,
        CardRowKind last)
    {
        var rows = BoardCardPlan.Rows(Issue("Short", []), part, Cell);

        Assert.Equal(first, rows[0].Kind);
        Assert.Equal(last, rows[^1].Kind);
    }

    [Fact]
    public void GivenANestedCard_WhenAskingTheTextWidth_ThenTheIndentComesOff()
    {
        Assert.Equal(34, BoardCardPlan.TextWidth(Cell, false));
        Assert.Equal(32, BoardCardPlan.TextWidth(Cell, true));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(60)]
    public void GivenAnyWidth_WhenPlanning_ThenTheCardNeverExceedsMaxRows(int cell)
    {
        var labels = Enumerable.Range(0, 20).Select(static number => $"lbl{number}").ToList();

        var rows = BoardCardPlan.Rows(Issue(new string('w', 300), labels), CardGroupPart.None, cell);

        Assert.InRange(rows.Count, 5, BoardCardPlan.MaxRows);
    }

    private static IssueSummary Issue(string summary, IReadOnlyList<string> labels)
    {
        return new IssueSummary(
            "A-1",
            "A-1",
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
