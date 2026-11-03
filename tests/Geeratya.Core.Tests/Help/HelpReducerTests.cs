using Geeratya.Core.Help;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;

namespace Geeratya.Core.Tests.Help;

/// <summary>Pages, filter and the two-step Esc of the help overlay, through the real reducer.</summary>
[Trait("Os", "Independent")]
public class HelpReducerTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly KeymapTable _keymap = DefaultKeymap.Load();
    private readonly AppReducer _reducer;

    public HelpReducerTests()
    {
        _reducer = new AppReducer(_keymap);
    }

    [Fact]
    public void GivenHelpOpen_WhenPressingTab_ThenTheNextPageShowsFromItsFirstLine()
    {
        var state = Press(Press(Opened(), "j"), "Tab");

        Assert.Equal(1, state.HelpPage);
        Assert.Equal(0, state.HelpScroll);
    }

    [Fact]
    public void GivenTheFirstPage_WhenPressingShiftTab_ThenItWrapsToTheLastPage()
    {
        var state = Press(Opened(), "Shift-Tab");

        Assert.Equal(HelpPages.Names(state, _keymap).Count - 1, state.HelpPage);
        Assert.Equal(HelpPages.CommandsName, HelpPages.Names(state, _keymap)[state.HelpPage]);
    }

    [Fact]
    public void GivenTheLastPage_WhenPressingTab_ThenItWrapsToTheFirstPage()
    {
        var state = Press(Press(Press(Opened(), "Tab"), "Tab"), "Tab");

        Assert.Equal(0, state.HelpPage);
    }

    [Fact]
    public void GivenAClickOnAPageName_WhenReducing_ThenThatPageShows()
    {
        var clicked = new OverlayRowClicked(MouseTarget.HelpPage, 2, ActionIds.HelpPageNext, _now);

        var state = _reducer.Reduce(Opened(), clicked).State;

        Assert.Equal(2, state.HelpPage);
    }

    [Fact]
    public void GivenSlash_WhenTyping_ThenPrintableKeysAreFilterTextEvenIfBound()
    {
        var state = Press(Press(Press(Opened(), "/"), "q"), "j");

        Assert.Equal(InputMode.Help, state.Mode);
        Assert.True(state.HelpFiltering);
        Assert.Equal("qj", state.HelpFilter);
        Assert.Equal(0, state.HelpScroll);
    }

    [Fact]
    public void GivenAFilterBeingTyped_WhenPressingBackspace_ThenTheLastCharacterGoes()
    {
        var state = Press(Press(Press(Press(Opened(), "/"), "a"), "b"), "Backspace");

        Assert.Equal("a", state.HelpFilter);
        Assert.True(state.HelpFiltering);
    }

    [Fact]
    public void GivenAKeptFilter_WhenPressingJ_ThenThePageScrollsAndTheFilterStays()
    {
        var state = Press(Press(Press(Opened(), "/"), "a"), "Enter");

        state = Press(state, "j");

        Assert.False(state.HelpFiltering);
        Assert.Equal("a", state.HelpFilter);
        Assert.Equal(1, state.HelpScroll);
    }

    [Fact]
    public void GivenAKeptFilter_WhenPressingEscTwice_ThenTheFirstClearsAndTheSecondClosesHelp()
    {
        var kept = Press(Press(Press(Opened(), "/"), "a"), "Enter");

        var cleared = Press(kept, "Esc");
        var closed = Press(cleared, "Esc");

        Assert.Equal(InputMode.Help, cleared.Mode);
        Assert.Equal(string.Empty, cleared.HelpFilter);
        Assert.Equal(InputMode.Normal, closed.Mode);
    }

    [Fact]
    public void GivenAFilterBeingTyped_WhenPressingEsc_ThenItClearsAndHelpStaysOpen()
    {
        var state = Press(Press(Press(Opened(), "/"), "a"), "Esc");

        Assert.Equal(InputMode.Help, state.Mode);
        Assert.False(state.HelpFiltering);
        Assert.Equal(string.Empty, state.HelpFilter);
    }

    [Fact]
    public void GivenAKeptFilter_WhenPressingQ_ThenItClearsTheFilterFirst()
    {
        var kept = Press(Press(Press(Opened(), "/"), "a"), "Enter");

        var state = Press(kept, "q");

        Assert.Equal(InputMode.Help, state.Mode);
        Assert.Equal(string.Empty, state.HelpFilter);
    }

    [Fact]
    public void GivenAFilterBeingTyped_WhenPressingCtrlC_ThenItQuits()
    {
        var typing = Press(Opened(), "/");

        var result = _reducer.Reduce(typing, new KeyPressed("Ctrl-c"));

        Assert.Contains(result.Effects, effect => effect is QuitEffect);
    }

    [Fact]
    public void GivenAFilterAndAPage_WhenClosingHelp_ThenTheNextOpeningStartsClean()
    {
        var state = Press(Press(Press(Press(Opened(), "Tab"), "/"), "a"), "Enter");

        var reopened = Press(Press(Press(state, "?"), "?"), "Tab");

        Assert.Equal(1, reopened.HelpPage);
        Assert.Equal(string.Empty, reopened.HelpFilter);
    }

    [Fact]
    public void GivenTheMessagesOverlay_WhenPressingTabAndSlash_ThenNothingChanges()
    {
        var messages = StatusMessages.OpenHistory(AppState.Initial);

        var state = Press(Press(messages, "Tab"), "/");

        Assert.Equal(0, state.HelpPage);
        Assert.False(state.HelpFiltering);
    }

    [Fact]
    public void GivenAFilterThatNarrowsThePage_WhenScrollingFarDown_ThenTheScrollStopsAtTheShorterLastLine()
    {
        var typed = Press(Press(Press(Press(Opened(), "/"), "q"), "u"), "Enter");
        var lines = HelpPages.Lines(typed, _keymap).Count;

        var state = typed;

        for (var step = 0; step < lines + 5; step++)
        {
            state = Press(state, "j");
        }

        Assert.Equal(lines - 1, state.HelpScroll);
    }

    private AppState Opened()
    {
        return Press(AppState.Initial, "?");
    }

    private AppState Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key)).State;
    }
}
