using Geeratya.Core.Boards;

namespace Geeratya.Core.Tests.Boards;

[Trait("Os", "Independent")]
public class BoardCardsTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(6, 7)]
    [InlineData(8, 9)]
    [InlineData(9, 2)]
    [InlineData(17, 3)]
    [InlineData(31, 6)]
    public void GivenCardAreaHeight_WhenCounting_ThenCompactBelowTwelveBodyRowsElseFiveRowsPerCard(
        int rows,
        int expected)
    {
        Assert.Equal(expected, BoardCards.Visible(rows, 2));
    }

    [Fact]
    public void GivenTheCompactThreshold_WhenAskingThePitch_ThenFullCardsStartAtTwelveBodyRows()
    {
        Assert.Equal(5, BoardCards.Pitch(9, 2));
        Assert.Equal(1, BoardCards.Pitch(8, 2));
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
