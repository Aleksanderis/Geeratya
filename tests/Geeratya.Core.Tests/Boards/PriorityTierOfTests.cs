using Geeratya.Core.Boards;

namespace Geeratya.Core.Tests.Boards;

[Trait("Os", "Independent")]
public class PriorityTierOfTests
{
    [Theory]
    [InlineData("Highest", PriorityTier.High)]
    [InlineData("High", PriorityTier.High)]
    [InlineData("Very high", PriorityTier.High)]
    [InlineData("Blocker", PriorityTier.High)]
    [InlineData("Critical", PriorityTier.High)]
    [InlineData("Medium", PriorityTier.Medium)]
    [InlineData("Major", PriorityTier.Medium)]
    [InlineData("Low", PriorityTier.Low)]
    [InlineData("Lowest", PriorityTier.Low)]
    [InlineData("Minor", PriorityTier.Low)]
    [InlineData("Trivial", PriorityTier.Low)]
    [InlineData("Urgent", PriorityTier.None)]
    [InlineData("P1", PriorityTier.None)]
    [InlineData("Highlight", PriorityTier.None)]
    [InlineData("  ", PriorityTier.None)]
    [InlineData(null, PriorityTier.None)]
    public void GivenAPriorityName_WhenMappingIt_ThenTheTierMatchesAndUnknownNamesHaveNone(
        string? name,
        PriorityTier expected)
    {
        Assert.Equal(expected, PriorityTierOf.Name(name));
    }
}
