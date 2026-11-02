using Geeratya.Core.Boards;

namespace Geeratya.Core.Tests.Boards;

[Trait("Os", "Independent")]
public class BoardCardsTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(6, 6)]
    [InlineData(9, 9)]
    [InlineData(10, 2)]
    [InlineData(20, 5)]
    [InlineData(34, 8)]
    public void GivenCardAreaHeight_WhenCounting_ThenCompactBelowTwelveBodyRowsElseFourRowsPerCard(
        int rows,
        int expected)
    {
        Assert.Equal(expected, BoardCards.Visible(rows, 2));
    }

    [Fact]
    public void GivenTheCompactThreshold_WhenAskingThePitch_ThenFullCardsStartAtTwelveBodyRows()
    {
        Assert.Equal(4, BoardCards.Pitch(10, 2));
        Assert.Equal(1, BoardCards.Pitch(9, 2));
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
