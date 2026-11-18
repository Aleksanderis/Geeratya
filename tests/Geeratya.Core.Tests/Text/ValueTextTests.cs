using Geeratya.Core.Text;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Text;

[Trait("Os", "Independent")]
public class ValueTextTests
{
    [Fact]
    public void GivenNoPerson_WhenBuildingTheChipAndTheName_ThenTheEmptyChipAndTheUnassignedWordAreMuted()
    {
        var spans = ValueText.Person(null, 2, TextRole.Body);

        Assert.Equal("[--]", spans[0].Text);
        Assert.Equal(TextRole.UnassignedChip, spans[0].Role);
        Assert.Equal(" Unassigned", spans[1].Text);
        Assert.Equal(TextRole.Muted, spans[1].Role);
    }

    [Fact]
    public void GivenAPerson_WhenBuildingTheChipAndTheName_ThenTheChipHasTheColourOfThePerson()
    {
        var spans = ValueText.Person("Jane Doe", 2, TextRole.ValueBlock);

        Assert.Equal(" JD ", spans[0].Text);
        Assert.Equal(PersonChips.For("Jane Doe"), spans[0].Role);
        Assert.Equal(" Jane Doe", spans[1].Text);
        Assert.Equal(TextRole.ValueBlock, spans[1].Role);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void GivenABlankName_WhenAskedForThePerson_ThenItIsEmpty(string? name)
    {
        Assert.False(ValueText.IsPerson(name));
        Assert.Equal("Unassigned", ValueText.PersonName(name));
    }

    [Theory]
    [InlineData(2, "[--]")]
    [InlineData(3, "[-- ]")]
    public void GivenTheEmptyChip_WhenTheCellsGrow_ThenItKeepsTheWidthOfAChipOfInitials(int cells, string expected)
    {
        Assert.Equal(expected, ValueText.PersonChip(null, cells).Text);
    }

    [Theory]
    [InlineData("Highest", "▲", TextRole.PriorityHigh)]
    [InlineData("Medium", "=", TextRole.PriorityMedium)]
    [InlineData("Lowest", "▼", TextRole.PriorityLow)]
    public void GivenAPriority_WhenBuilding_ThenTheGlyphAndTheRoleAreTheSameOnEverySurface(
        string name,
        string glyph,
        TextRole role)
    {
        var spans = ValueText.Priority(name, TextRole.ValueBlock);

        Assert.Equal(glyph, spans[0].Text);
        Assert.Equal(role, spans[0].Role);
        Assert.Equal($" {name}", spans[1].Text);
        Assert.Equal(glyph, ValueText.PriorityGlyph(name));
        Assert.Equal(role, ValueText.PriorityRole(name));
    }

    [Fact]
    public void GivenNoPriority_WhenBuilding_ThenTheNoneWordIsMuted()
    {
        var span = Assert.Single(ValueText.Priority(null, TextRole.ValueBlock));

        Assert.Equal("None", span.Text);
        Assert.Equal(TextRole.Muted, span.Role);
    }

    [Theory]
    [InlineData("new", TextRole.StatusToDo)]
    [InlineData("indeterminate", TextRole.StatusInProgress)]
    [InlineData("done", TextRole.StatusDone)]
    public void GivenAStatus_WhenBuilding_ThenTheDotAndTheNameHaveTheRoleOfItsCategory(string category, TextRole role)
    {
        var status = new IssueStatus("1", "Open", category);

        Assert.All(ValueText.Status(status), span => Assert.Equal(role, span.Role));
        Assert.Equal(role, ValueText.StatusRole(status));
    }

    [Theory]
    [InlineData(5.0, "5")]
    [InlineData(0.25, "0.25")]
    [InlineData(0.5, "0.5")]
    public void GivenAnEstimate_WhenFormatted_ThenWholeNumbersHaveNoFractionAndOthersTwoDecimalsAtMost(
        double estimate,
        string expected)
    {
        Assert.Equal(expected, ValueText.Estimate(estimate));
        Assert.Equal(expected, ValueText.Estimate((double?)estimate));
    }

    [Fact]
    public void GivenNoEstimate_WhenFormatted_ThenItIsEmpty()
    {
        Assert.Equal(string.Empty, ValueText.Estimate(null));
    }

    [Fact]
    public void GivenTwoDates_WhenBuildingARange_ThenItReadsDayMonthAndEmptyWhenAnEndIsMissing()
    {
        var start = new DateTimeOffset(2026, 11, 3, 8, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 11, 17, 8, 0, 0, TimeSpan.Zero);

        Assert.Equal("3 Nov - 17 Nov", ValueText.DateRange(start, end));
        Assert.Equal(string.Empty, ValueText.DateRange(start, null));
    }

    [Fact]
    public void GivenADueDate_WhenFormatted_ThenItIsIsoAndTheNoneWordWhenMissing()
    {
        Assert.Equal("2026-11-17", ValueText.Due(new DateOnly(2026, 11, 17)));
        Assert.Equal("None", ValueText.Due(null));
    }
}
