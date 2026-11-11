using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class MarkSetTests
{
    [Fact]
    public void GivenTheSameMarkList_WhenAskingTwice_ThenTheSetIsBuiltOnce()
    {
        IReadOnlyList<string> marks = ["AB-1", "AB-2"];

        Assert.Same(MarkSet.Of(marks), MarkSet.Of(marks));
    }

    [Fact]
    public void GivenAReplacedMarkList_WhenAsking_ThenTheNewListIsUsed()
    {
        IReadOnlyList<string> first = ["AB-1"];
        IReadOnlyList<string> second = ["AB-2"];

        Assert.DoesNotContain("AB-2", MarkSet.Of(first));
        Assert.Contains("AB-2", MarkSet.Of(second));
    }

    [Fact]
    public void GivenMarks_WhenLookingUpInOtherCase_ThenTheKeyIsFound()
    {
        IReadOnlyList<string> marks = ["ab-1"];

        Assert.Contains("AB-1", MarkSet.Of(marks));
    }

    [Fact]
    public void GivenNoMarks_WhenAsking_ThenTheSetIsEmpty()
    {
        Assert.Empty(MarkSet.Of([]));
    }
}
