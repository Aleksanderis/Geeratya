using Geeratya.Core.Mouse;
using Geeratya.Tui.Input;
using Terminal.Gui.Input;

namespace Geeratya.Tui.Tests.Input;

[Trait("Os", "Independent")]
public class MouseFlagTranslatorTests
{
    [Theory]
    [InlineData(MouseFlags.WheeledUp, MouseGesture.WheelUp)]
    [InlineData(MouseFlags.WheeledDown, MouseGesture.WheelDown)]
    [InlineData(MouseFlags.LeftButtonClicked, MouseGesture.Click)]
    [InlineData(MouseFlags.LeftButtonDoubleClicked, MouseGesture.DoubleClick)]
    [InlineData(MouseFlags.RightButtonClicked, MouseGesture.RightClick)]
    [InlineData(MouseFlags.MiddleButtonClicked, MouseGesture.MiddleClick)]
    public void GivenFlags_WhenTranslating_ThenGesture(MouseFlags flags, MouseGesture expected)
    {
        Assert.True(MouseFlagTranslator.TryTranslate(flags, out var gesture));
        Assert.Equal(expected, gesture);
    }

    [Fact]
    public void GivenCtrlClick_WhenTranslating_ThenCtrlClick()
    {
        Assert.True(MouseFlagTranslator.TryTranslate(MouseFlags.LeftButtonClicked | MouseFlags.Ctrl, out var gesture));
        Assert.Equal(MouseGesture.CtrlClick, gesture);
    }

    [Theory]
    [InlineData(MouseFlags.LeftButtonPressed)]
    [InlineData(MouseFlags.LeftButtonReleased)]
    [InlineData(MouseFlags.PositionReport)]
    [InlineData(MouseFlags.WheeledUp | MouseFlags.Shift)]
    public void GivenPressMoveOrSidewaysWheel_WhenTranslating_ThenIgnored(MouseFlags flags)
    {
        Assert.False(MouseFlagTranslator.TryTranslate(flags, out _));
    }

    [Theory]
    [InlineData(MouseFlags.PositionReport, true)]
    [InlineData(MouseFlags.PositionReport | MouseFlags.LeftButtonPressed, false)]
    [InlineData(MouseFlags.PositionReport | MouseFlags.MiddleButtonPressed, false)]
    [InlineData(MouseFlags.LeftButtonClicked, false)]
    public void GivenFlags_WhenAskingForHover_ThenOnlyAMoveWithNoButtonIs(MouseFlags flags, bool expected)
    {
        Assert.Equal(expected, MouseFlagTranslator.IsHover(flags));
    }
}
