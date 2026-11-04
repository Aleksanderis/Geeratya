using Geeratya.Core.Boards;
using Geeratya.Core.Text;
using Geeratya.Jira.Search.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class CardRowsTests
{
    [Theory]
    [InlineData(40)]
    [InlineData(24)]
    [InlineData(12)]
    [InlineData(6)]
    public void GivenLabels_WhenBuildingTheRowAtAnyWidth_ThenItIsExactlyAsWideAsAsked(int width)
    {
        var row = Row(["alpha", "beta", "gamma-long-label", "d"], width);

        Assert.Equal(width, Width(row));
    }

    [Fact]
    public void GivenLabels_WhenTheyFit_ThenEachIsAChipInItsStableColour()
    {
        var row = Row(["alpha", "beta"], 40);

        var chips = row.Where(span => span.Text.StartsWith('[')).ToList();
        Assert.Equal(["[alpha]", "[beta]"], chips.Select(span => span.Text));
        Assert.Equal(LabelChips.For("alpha"), chips[0].Role);
        Assert.Equal(LabelChips.For("beta"), chips[1].Role);
        Assert.DoesNotContain(row, span => span.Text.Contains('+'));
    }

    [Fact]
    public void GivenMoreLabelsThanFit_WhenBuildingTheRow_ThenTheRestIsCountedAsPlusN()
    {
        var chips = new List<CardLabelChip> { new("alpha", "[alpha]"), new("beta", "[beta]") };
        var row = CardRows.Labels(new CardLabelRow(chips, 2), 22);

        Assert.Equal("[alpha] [beta] +2", Text(row).TrimEnd());
        Assert.Equal(TextRole.Muted, row.Single(span => span.Text.Contains('+')).Role);
    }

    [Fact]
    public void GivenALabelWiderThanTheRow_WhenBuildingTheRow_ThenItIsCutWithAnEllipsis()
    {
        var row = Row(["a-very-long-label-name"], 12);

        Assert.Equal("[a-very-lo…]", Text(row).TrimEnd());
    }

    [Fact]
    public void GivenAnAssignee_WhenBuildingTheBottomRow_ThenKeyTypeWordPriorityAndInitialsChipShow()
    {
        var issue = Issue() with { Priority = "High", Assignee = new IssueAssignee("a", "Jane Doe") };

        var row = CardRows.Bottom(issue, false, 32, TextRole.Body);

        Assert.Equal("GEN-1  Task               ▲ [JD]", Text(row));
        Assert.Equal(TextRole.PriorityHigh, row.Single(span => span.Text == "▲").Role);
        Assert.Equal(TextRole.InitialsChip, row.Single(span => span.Text == "[JD]").Role);
        Assert.Equal(TextRole.TypeTask, row.Single(span => span.Text == "Task").Role);
    }

    [Fact]
    public void GivenNoAssignee_WhenBuildingTheBottomRow_ThenTheDashChipSitsInTheSamePlaceAndWidthAsInitials()
    {
        var jane = Issue() with { Assignee = new IssueAssignee("a", "Jane Doe") };
        var assigned = CardRows.Bottom(jane, false, 32, TextRole.Body);
        var unassigned = CardRows.Bottom(Issue(), false, 32, TextRole.Body);

        Assert.Equal(Text(assigned).Length, Text(unassigned).Length);
        Assert.EndsWith("[--]", Text(unassigned));
        Assert.Equal(TextRole.UnassignedChip, unassigned.Single(span => span.Text == "[--]").Role);
    }

    [Fact]
    public void GivenAnUnknownPriorityName_WhenBuildingTheBottomRow_ThenNoGlyphIsGuessed()
    {
        var row = CardRows.Bottom(Issue() with { Priority = "Urgent" }, false, 32, TextRole.Body);

        Assert.DoesNotContain(row, span => span.Text is "▲" or "=" or "▼");
    }

    [Fact]
    public void GivenOneInitial_WhenBuildingTheBottomRow_ThenTheChipKeepsItsWidth()
    {
        var single = Issue() with { Assignee = new IssueAssignee("a", "Madonna") };

        var row = CardRows.Bottom(single, false, 32, TextRole.Body);

        Assert.EndsWith("[M ]", Text(row));
    }

    [Theory]
    [InlineData("Highest", "▲")]
    [InlineData("Medium", "=")]
    [InlineData("Lowest", "▼")]
    [InlineData("Major", "=")]
    public void GivenAPriorityName_WhenBuildingTheBottomRow_ThenTheTierGlyphShows(string priority, string glyph)
    {
        var row = CardRows.Bottom(Issue() with { Priority = priority }, false, 32, TextRole.Body);

        Assert.Contains(glyph, Text(row));
    }

    [Fact]
    public void GivenSubtasks_WhenThereIsRoom_ThenTheCounterFollowsTheTypeWordAndItYieldsWhenThereIsNone()
    {
        var issue = Issue() with { Subtasks = [Subtask("GEN-2", "done"), Subtask("GEN-3", "new")] };

        var wide = CardRows.Bottom(issue, false, 32, TextRole.Body);
        var open = CardRows.Bottom(issue, true, 32, TextRole.Body);
        var tight = CardRows.Bottom(issue, false, 18, TextRole.Body);

        Assert.StartsWith("GEN-1  Task ▸1/2", Text(wide));
        Assert.StartsWith("GEN-1  Task ▾1/2", Text(open));
        Assert.DoesNotContain("1/2", Text(tight));
    }

    [Theory]
    [InlineData(40)]
    [InlineData(24)]
    [InlineData(16)]
    [InlineData(12)]
    [InlineData(8)]
    [InlineData(4)]
    public void GivenAnyWidth_WhenBuildingTheBottomRow_ThenItIsExactlyAsWideAsAsked(int width)
    {
        var issue = Issue() with
        {
            Priority = "High",
            Assignee = new IssueAssignee("a", "Jane Doe"),
            Estimate = 3,
            Subtasks = [Subtask("GEN-2", "new")],
        };

        Assert.Equal(width, Width(CardRows.Bottom(issue, false, width, TextRole.Body)));
    }

    private static List<StyledSpan> Row(string[] labels, int width)
    {
        return CardRows.Labels(CardLabelLayout.Rows(labels, width)[0], width);
    }

    private static string Text(List<StyledSpan> row)
    {
        return string.Concat(row.Select(span => span.Text));
    }

    private static int Width(List<StyledSpan> row)
    {
        return TextWidth.Of(Text(row));
    }

    private static IssueSubtask Subtask(string key, string category)
    {
        return new IssueSubtask(key, "S", new IssueStatus("1", "S", category), "Sub-task");
    }

    private static IssueStatus Status()
    {
        return new IssueStatus("1", "To Do", "new");
    }

    private static IssueSummary Issue()
    {
        return new IssueSummary(
            "GEN-1",
            "GEN-1",
            "Summary",
            Status(),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
    }
}
