using Geeratya.Core.Boards;
using Geeratya.Core.Detail;
using Geeratya.Core.Sprints;
using Geeratya.Core.Text;

namespace Geeratya.Core.Tests.Text;

public sealed class EmptyValuesTests
{
    [Fact]
    public void GivenTheEmptyWords_WhenRead_ThenPeopleAreUnassignedAndEverythingElseIsNone()
    {
        Assert.Equal("Unassigned", EmptyValues.Unassigned);
        Assert.Equal("None", EmptyValues.None);
    }

    [Theory]
    [InlineData(0.25, "0.25")]
    [InlineData(5.0, "5")]
    [InlineData(0.5, "0.5")]
    public void GivenAnEstimate_WhenFormatted_ThenOneFormatServesEverySurface(double estimate, string expected)
    {
        Assert.Equal(expected, EstimateText.Format(estimate));
        Assert.Equal(expected, SprintHeaderText.Estimate(estimate));
        Assert.Equal(expected, SprintTally.Number(estimate));
    }
}
