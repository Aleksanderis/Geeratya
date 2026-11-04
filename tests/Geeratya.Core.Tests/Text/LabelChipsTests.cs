using Geeratya.Core.Text;

namespace Geeratya.Core.Tests.Text;

[Trait("Os", "Independent")]
public class LabelChipsTests
{
    [Theory]
    [InlineData("backend", TextRole.LabelChip2)]
    [InlineData("frontend", TextRole.LabelChip1)]
    [InlineData("urgent", TextRole.LabelChip1)]
    public void GivenLabelText_WhenPickingItsChip_ThenTheRoleIsThePinnedOne(string label, TextRole expected)
    {
        Assert.Equal(expected, LabelChips.For(label));
    }

    [Fact]
    public void GivenSameLabelInOtherCase_WhenPickingItsChip_ThenTheRoleIsTheSame()
    {
        Assert.Equal(LabelChips.For("Backend"), LabelChips.For("BACKEND"));
    }
}
