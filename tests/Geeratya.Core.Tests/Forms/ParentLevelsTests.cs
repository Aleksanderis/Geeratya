using Geeratya.Core.Forms;

namespace Geeratya.Core.Tests.Forms;

[Trait("Os", "Independent")]
public sealed class ParentLevelsTests
{
    [Theory]
    [InlineData(0, false, 1)]
    [InlineData(1, false, 2)]
    [InlineData(-1, true, 0)]
    [InlineData(-1, false, 0)]
    [InlineData(0, true, 0)]
    public void GivenAnIssueType_WhenAskingForTheAcceptedParentLevel_ThenItIsTheLevelAbove(
        int level,
        bool isSubtask,
        int expected)
    {
        Assert.Equal(expected, ParentLevels.Accepted(level, isSubtask));
    }
}
