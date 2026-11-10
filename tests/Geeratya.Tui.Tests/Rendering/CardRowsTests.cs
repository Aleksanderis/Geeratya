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

        var chips = row.Where(static span => span.Text.StartsWith(' ') && span.Text.Trim().Length > 0).ToList();
        Assert.Equal([" alpha ", " beta "], chips.Select(span => span.Text));
        Assert.Equal(LabelChips.For("alpha"), chips[0].Role);
        Assert.Equal(LabelChips.For("beta"), chips[1].Role);
        Assert.DoesNotContain(row, span => span.Text.Contains('+'));
    }

    [Fact]
    public void GivenMoreLabelsThanFit_WhenBuildingTheRow_ThenTheRestIsCountedAsPlusN()
    {
        var chips = new List<CardLabelChip> { new("alpha", " alpha "), new("beta", " beta ") };
        var row = CardRows.Labels(new CardLabelRow(chips, 2), 22);

        Assert.Equal(" alpha   beta  +2", Text(row).TrimEnd());
        Assert.Equal(TextRole.Muted, row.Single(span => span.Text.Contains('+')).Role);
    }

    [Fact]
    public void GivenALabelWiderThanTheRow_WhenBuildingTheRow_ThenItIsCutWithAnEllipsis()
    {
        var row = Row(["a-very-long-label-name"], 12);

        Assert.Equal(" a-very-lo… ", Text(row)[..12]);
    }

    [Theory]
    [InlineData("Highest", "▲", TextRole.PriorityHigh)]
    [InlineData("Major", "=", TextRole.PriorityMedium)]
    [InlineData("Medium", "=", TextRole.PriorityMedium)]
    [InlineData("Minor", "▼", TextRole.PriorityLow)]
    [InlineData("Trivial", "▼", TextRole.PriorityLow)]
    public void GivenAPriority_WhenBuildingTheBottomRow_ThenTheGlyphHasItsOwnRole(
        string priority,
        string glyph,
        TextRole role)
    {
        var row = CardRows.Bottom(Issue() with { Priority = priority }, false, 32, TextRole.Body);

        Assert.Equal(role, row.Single(span => span.Text == glyph).Role);
    }

    [Fact]
    public void GivenAnAssignee_WhenBuildingTheBottomRow_ThenKeyTypeWordPriorityAndInitialsChipShow()
    {
        var issue = Issue() with { Priority = "High", Assignee = new IssueAssignee("a", "Jane Doe") };

        var row = CardRows.Bottom(issue, false, 32, TextRole.Body);

        Assert.Equal("GEN-1  Task               ▲  JD ", Text(row));
        Assert.Equal(TextRole.PriorityHigh, row.Single(span => span.Text == "▲").Role);
        Assert.Equal(PersonChips.For("Jane Doe"), row.Single(span => span.Text == " JD ").Role);
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
    public void GivenAListedSubtaskWithKnownAssignee_WhenBuildingTheBottomRow_ThenTheChipSitsRightAfterTheStatus()
    {
        var jane = Listed() with { Assignee = new IssueAssignee("a", "Jane Doe"), AssigneeLoaded = true };
        var unassigned = Listed() with { AssigneeLoaded = true };

        var assigned = CardRows.Bottom(jane, false, 32, TextRole.Body);

        Assert.EndsWith("[To Do]  JD ", Text(assigned));
        Assert.Equal(PersonChips.For("Jane Doe"), assigned.Single(span => span.Text == " JD ").Role);
        Assert.EndsWith("[To Do] [--]", Text(CardRows.Bottom(unassigned, false, 32, TextRole.Body)));
        Assert.Equal(32, Width(assigned));
    }

    [Fact]
    public void GivenAListedSubtaskWithUnknownAssignee_WhenBuildingTheBottomRow_ThenOnlyTheStatusShows()
    {
        var row = CardRows.Bottom(Listed(), false, 32, TextRole.Body);

        Assert.EndsWith("[To Do]", Text(row));
        Assert.DoesNotContain("[--]", Text(row));
    }

    [Fact]
    public void GivenAListedSubtaskInANarrowCard_WhenBuildingTheBottomRow_ThenTheStatusIsCutBeforeThePersonIsDropped()
    {
        var issue = Listed() with
        {
            Status = new IssueStatus("1", "In Progress", "indeterminate"),
            Assignee = new IssueAssignee("a", "Jane Doe"),
            AssigneeLoaded = true,
        };

        var row = CardRows.Bottom(issue, false, 24, TextRole.Body);

        Assert.Contains(" JD ", Text(row));
        Assert.Equal(24, Width(row));
    }

    [Fact]
    public void GivenAnUnknownPriorityName_WhenBuildingTheBottomRow_ThenNoGlyphIsGuessed()
    {
        var row = CardRows.Bottom(Issue() with { Priority = "Urgent" }, false, 32, TextRole.Body);

        Assert.DoesNotContain(row, span => span.Text is "▲" or "=" or "▼");
    }

    [Fact]
    public void GivenAnAssigneeChip_WhenApplyingCaps_ThenItIsRoundedInThePersonsColourAndKeepsItsWidth()
    {
        var issue = Issue() with { Assignee = new IssueAssignee("a", "Jane Doe") };
        var row = new StyledLine(CardRows.Bottom(issue, false, 32, TextRole.Body));

        var rounded = ChipCaps.Apply(row);

        var chip = PersonChips.For("Jane Doe");
        var cap = ChipCaps.CapRole(chip)!.Value;

        Assert.Contains(rounded.Spans, span => span.Text == ChipCaps.Left && span.Role == cap);
        Assert.Contains(rounded.Spans, span => span.Text == "JD" && span.Role == chip);
        Assert.Equal(TextWidth.Of(row.PlainText), TextWidth.Of(rounded.PlainText));
    }

    [Fact]
    public void GivenOneInitial_WhenBuildingTheBottomRow_ThenTheChipKeepsItsWidth()
    {
        var single = Issue() with { Assignee = new IssueAssignee("a", "Madonna") };

        var row = CardRows.Bottom(single, false, 32, TextRole.Body);

        Assert.EndsWith(" M  ", Text(row));
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

    private static IssueSummary Listed()
    {
        return Issue() with { IsListed = true, IssueType = new IssueTypeSummary("", "Sub-task", -1, true) };
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
