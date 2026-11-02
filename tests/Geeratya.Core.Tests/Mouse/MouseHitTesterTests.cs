using Geeratya.Core.Mouse;

namespace Geeratya.Core.Tests.Mouse;

[Trait("Os", "Independent")]
public class MouseHitTesterTests
{
    [Theory]
    [InlineData(80, 24)]
    [InlineData(40, 12)]
    [InlineData(20, 6)]
    public void GivenSize_WhenLocatingEachRegion_ThenFindsTabsBannerBodyAndStatus(int width, int height)
    {
        var context = new MouseContext(width, height, 2, false, false);

        Assert.Equal(MouseTarget.Tabs, MouseHitTester.Locate(context, 0, 0)!.Target);
        Assert.Equal(MouseTarget.Banner, MouseHitTester.Locate(context, width - 1, 1)!.Target);
        Assert.Equal(new MouseHit(MouseTarget.Lists, 0, 3), MouseHitTester.Locate(context, 3, 2));
        Assert.Equal(new MouseHit(MouseTarget.Lists, height - 4, 0), MouseHitTester.Locate(context, 0, height - 2));
        Assert.Equal(MouseTarget.Status, MouseHitTester.Locate(context, 1, height - 1)!.Target);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(80, 0)]
    [InlineData(0, 24)]
    public void GivenCellOutsideScreen_WhenLocating_ThenNull(int column, int row)
    {
        Assert.Null(MouseHitTester.Locate(new MouseContext(80, 24, 2, false, false), column, row));
    }

    [Theory]
    [InlineData(80, 24)]
    [InlineData(40, 12)]
    [InlineData(20, 6)]
    public void GivenOverlayWithRegions_WhenLocating_ThenTheTopmostRowWinsAndTheRestOfTheBodyIsNothing(
        int width,
        int height)
    {
        OverlayRegion[] regions =
        [
            new(MouseTarget.PickerRow, 3, 1, width - 2, 1, 0),
            new(MouseTarget.PickerRow, 3, 1, width - 2, 1, 5),
        ];
        var context = new MouseContext(width, height, 2, true, false, regions);

        Assert.Equal(new MouseHit(MouseTarget.PickerRow, 5, 1), MouseHitTester.Locate(context, 1, 3));
        Assert.Null(MouseHitTester.Locate(context, 0, 3));
        Assert.Null(MouseHitTester.Locate(context, 1, 4));
        Assert.Equal(MouseTarget.Backdrop, MouseHitTester.Locate(context, 1, 0)!.Target);
    }

    [Fact]
    public void GivenRegionsButNoOverlay_WhenLocating_ThenRegionsAreIgnored()
    {
        OverlayRegion[] regions = [new(MouseTarget.PickerRow, 3, 1, 10, 1, 0)];
        var context = new MouseContext(80, 24, 2, false, false, regions);

        Assert.Equal(MouseTarget.Lists, MouseHitTester.Locate(context, 1, 3)!.Target);
    }

    [Fact]
    public void GivenOpenedIssue_WhenLocatingInBody_ThenTargetIsDetail()
    {
        var hit = MouseHitTester.Locate(new MouseContext(80, 24, 2, false, true), 5, 10);

        Assert.Equal(MouseTarget.Detail, hit!.Target);
        Assert.Equal(8, hit.Row);
        Assert.Equal(5, hit.Column);
    }

    [Fact]
    public void GivenOverlay_WhenLocating_ThenHeaderIsBackdropAndBodyAndStatusAreIgnored()
    {
        var context = new MouseContext(80, 24, 2, true, false);

        Assert.Equal(MouseTarget.Backdrop, MouseHitTester.Locate(context, 4, 0)!.Target);
        Assert.Equal(MouseTarget.Backdrop, MouseHitTester.Locate(context, 4, 1)!.Target);
        Assert.Null(MouseHitTester.Locate(context, 4, 10));
        Assert.Null(MouseHitTester.Locate(context, 4, 23));
    }

    [Fact]
    public void GivenOneRowScreen_WhenLocating_ThenOnlyTheStatusLineExists()
    {
        var context = new MouseContext(10, 1, 2, false, false);

        Assert.Equal(MouseTarget.Status, MouseHitTester.Locate(context, 0, 0)!.Target);
    }
}
