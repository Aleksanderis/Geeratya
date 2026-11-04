using Geeratya.Core.Boards;

namespace Geeratya.Core.Tests.Boards;

[Trait("Os", "Independent")]
public class BoardCardsTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(6, 7)]
    [InlineData(8, 9)]
    [InlineData(9, 1)]
    [InlineData(17, 2)]
    [InlineData(31, 3)]
    public void GivenCardAreaHeight_WhenCounting_ThenCompactBelowTwelveBodyRowsElseNineRowsPerCard(
        int rows,
        int expected)
    {
        Assert.Equal(expected, BoardCards.Visible(rows, 2));
    }

    [Theory]
    [InlineData("Jane Doe", "JD")]
    [InlineData("ola nordmann", "ON")]
    [InlineData("Madonna", "M")]
    [InlineData("Anna-Maria Lopez Garcia", "AG")]
    [InlineData("   ", "")]
    [InlineData("", "")]
    [InlineData("😀 Smile Üwe", "😀Ü")]
    public void GivenDisplayName_WhenTakingInitials_ThenFirstAndLastWordLettersInUpperCase(string name, string expected)
    {
        Assert.Equal(expected, AssigneeInitials.Of(name));
    }
}
