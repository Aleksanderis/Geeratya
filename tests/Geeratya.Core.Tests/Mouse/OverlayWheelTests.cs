using Geeratya.Core.Keymap;
using Geeratya.Core.Menus;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;

namespace Geeratya.Core.Tests.Mouse;

/// <summary>The wheel and the end jumps in the Help and Messages overlays and the context menus, through the resolver and the reducer.</summary>
[Trait("Os", "Independent")]
public class OverlayWheelTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenHelpOpen_WhenWheelingDownAnywhere_ThenThePageScrollsThreeLines()
    {
        var state = Press(AppState.Initial, "?");

        var scrolled = Wheel(state, MouseGesture.WheelDown, 1, 5);
        var back = Wheel(scrolled, MouseGesture.WheelUp, 70, 20);

        Assert.Equal(3, scrolled.HelpScroll);
        Assert.Equal(0, back.HelpScroll);
        Assert.Equal(state.HelpPage, scrolled.HelpPage);
    }

    [Fact]
    public void GivenMessagesOpen_WhenWheeling_ThenTheHistoryScrollsAndStopsAtTheEnds()
    {
        var history = Enumerable.Range(0, 6).Select(n => new StatusMessage($"Message {n}", StatusSeverity.Info)).ToList();
        var state = Press(Press(AppState.Initial with { StatusHistory = history }, "Space"), "n");

        var down = Wheel(state, MouseGesture.WheelDown, 10, 10);
        var past = Wheel(Wheel(down, MouseGesture.WheelDown, 10, 10), MouseGesture.WheelDown, 10, 10);
        var up = Wheel(Wheel(past, MouseGesture.WheelUp, 10, 10), MouseGesture.WheelUp, 10, 10);

        Assert.True(state.MessagesOpen);
        Assert.Equal(3, down.HelpScroll);
        Assert.True(past.HelpScroll <= 5);
        Assert.Equal(0, up.HelpScroll);
    }

    [Fact]
    public void GivenAContextMenu_WhenWheeling_ThenTheHighlightMovesThreeAndNothingRuns()
    {
        var items = Enumerable.Range(0, 8).Select(n => new ContextMenuItem(ActionIds.IssueEdit, $"Item {n}")).ToList();
        var state = AppState.Initial with { Mode = InputMode.Insert, ContextMenu = new ContextMenuState(items, 0) };

        var down = Wheel(state, MouseGesture.WheelDown, 4, 4);
        var up = Wheel(down, MouseGesture.WheelUp, 4, 4);

        Assert.Equal(3, down.ContextMenu!.Selected);
        Assert.Equal(0, up.ContextMenu!.Selected);
        Assert.Equal(InputMode.Insert, down.Mode);
    }

    [Fact]
    public void GivenHelpScrolled_WhenPressingGAndGg_ThenItJumpsToTheLastLineAndBackToTheTop()
    {
        var state = Press(Press(Press(Press(AppState.Initial, "?"), "J"), "J"), "J");

        var bottom = Press(state, "G");
        var top = Press(Press(bottom, "g"), "g");

        Assert.True(bottom.HelpScroll > 3);
        Assert.Equal(0, top.HelpScroll);
    }

    [Fact]
    public void GivenHelpFilterIsOpen_WhenTypingG_ThenItIsTextAndDoesNotScroll()
    {
        var state = Press(Press(AppState.Initial, "?"), "/");

        var typed = Press(Press(state, "G"), "g");

        Assert.Equal(0, typed.HelpScroll);
        Assert.Equal("Gg", typed.HelpFilter);
    }

    [Fact]
    public void GivenTheState_WhenBuildingTheContext_ThenOnlyHelpMessagesAndMenusHaveAWheelTarget()
    {
        Assert.Null(MouseContext.From(AppState.Initial, 80, 24, 2, _now).WheelTarget);
        Assert.Equal(MouseTarget.HelpBody, MouseContext.From(Press(AppState.Initial, "?"), 80, 24, 2, _now).WheelTarget);
    }

    private AppState Wheel(AppState state, MouseGesture gesture, int column, int row)
    {
        var context = MouseContext.From(state, 80, 24, 2, _now);

        return MouseResolver.Resolve(context, column, row, gesture, _now)
            .Aggregate(state, (current, command) => _reducer.Reduce(current, command).State);
    }

    private AppState Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key)).State;
    }
}
