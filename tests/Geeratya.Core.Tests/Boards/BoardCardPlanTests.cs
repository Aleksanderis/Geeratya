using Geeratya.Core.Boards;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Boards;

[Trait("Os", "Independent")]
public class BoardCardPlanTests
{
    [Fact]
    public void GivenCardsWithAndWithoutLabels_WhenAskingThePlan_ThenALabelRowTakesTheSecondSummaryLine()
    {
        var plain = Issue([]);
        var labelled = Issue(["x"]);

        Assert.Equal(2, BoardCardPlan.SummaryLines(plain));
        Assert.False(BoardCardPlan.HasLabelRow(plain));
        Assert.Equal(1, BoardCardPlan.SummaryLines(labelled));
        Assert.True(BoardCardPlan.HasLabelRow(labelled));
    }

    private static IssueSummary Issue(IReadOnlyList<string> labels)
    {
        return new IssueSummary(
            "A-1",
            "A-1",
            "Summary",
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
