using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Tests.Mouse;

public class LaneChipHitTests
{
    private const int Width = 80;
    private const int Height = 24;

    [Theory]
    [InlineData(19, MouseTarget.Status)]
    [InlineData(20, MouseTarget.LaneChip)]
    [InlineData(29, MouseTarget.LaneChip)]
    [InlineData(30, MouseTarget.Status)]
    public void GivenALaneChip_WhenLocatingAroundIt_ThenOnlyItsCellsAreTheChip(int column, MouseTarget expected)
    {
        var hit = MouseHitTester.Locate(Context(false, false), column, Height - 1);

        Assert.Equal(expected, hit!.Target);
    }

    [Fact]
    public void GivenALaneChipOverTheMessageCells_WhenLocating_ThenTheChipWins()
    {
        var context = Context(false, false) with { StatusMessage = new TabButton(8, 60) };

        Assert.Equal(MouseTarget.LaneChip, MouseHitTester.Locate(context, 25, Height - 1)!.Target);
    }

    [Fact]
    public void GivenAnOverlayOrConfirm_WhenLocatingTheChipCells_ThenItIsNotTheChip()
    {
        Assert.Null(MouseHitTester.Locate(Context(true, false), 25, Height - 1));
        Assert.Equal(MouseTarget.Backdrop, MouseHitTester.Locate(Context(true, true), 25, Height - 1)!.Target);
    }

    [Fact]
    public void GivenALaneChip_WhenClickingOrRightClicking_ThenItStepsTheLaneOrOpensTheViewMenu()
    {
        var context = Context(false, false);
        var now = DateTimeOffset.UnixEpoch;

        var click = MouseResolver.Resolve(context, 25, Height - 1, MouseGesture.Click, now);
        var right = MouseResolver.Resolve(context, 25, Height - 1, MouseGesture.RightClick, now);

        Assert.Equal("detail.section-next", Assert.IsType<ActionRequested>(Assert.Single(click)).Action);
        Assert.Equal("menu.view", Assert.IsType<ActionRequested>(Assert.Single(right)).Action);
    }

    private static MouseContext Context(bool overlay, bool confirm)
    {
        return new MouseContext(Width, Height, 2, overlay, false)
        {
            LaneChip = new TabButton(20, 30),
            ConfirmOpen = confirm,
        };
    }
}
