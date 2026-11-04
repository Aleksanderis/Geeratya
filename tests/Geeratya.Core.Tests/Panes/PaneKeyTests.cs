using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Panes;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Panes;

/// <summary>Pane keys through the real reducer: Normal mode, zoom and Esc, and the form rule (decision 2026-11-04).</summary>
[Trait("Os", "Independent")]
public class PaneKeyTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private static readonly PaneGrid _grid = new(
    [
        new PaneSlot("title", 0, 0),
        new PaneSlot("description", 0, 1),
        new PaneSlot("details", 1, 0, 1, 2),
    ]);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Theory]
    [InlineData("Ctrl-j", "description")]
    [InlineData("Ctrl-w j", "description")]
    [InlineData("Ctrl-l", "details")]
    [InlineData("Ctrl-w l", "details")]
    public void GivenNormalMode_WhenAPaneKeyIsPressed_ThenTheFocusMoves(string keys, string expected)
    {
        var state = Press(WithPanes(), keys);

        Assert.Equal(expected, Panes(state).Focused);
    }

    [Theory]
    [InlineData("Ctrl-h")]
    [InlineData("Ctrl-Backspace")]
    [InlineData("Ctrl-w h")]
    public void GivenFocusOnTheSidebar_WhenALeftKeyIsPressed_ThenFocusGoesLeft(string keys)
    {
        var state = Press(WithPanes(), "Ctrl-l");

        Assert.Equal("title", Panes(Press(state, keys)).Focused);
    }

    [Theory]
    [InlineData("Ctrl-k")]
    [InlineData("Ctrl-w k")]
    public void GivenFocusOnDescription_WhenAnUpKeyIsPressed_ThenFocusGoesToTheTitle(string keys)
    {
        var state = Press(WithPanes(), "Ctrl-j");

        Assert.Equal("title", Panes(Press(state, keys)).Focused);
    }

    [Fact]
    public void GivenNoPanes_WhenPaneKeysArePressed_ThenTheTabKeepsNoPanes()
    {
        var state = Tabbed();

        foreach (var keys in new[] { "Ctrl-h", "Ctrl-j", "Ctrl-k", "Ctrl-l", "Ctrl-w l", "Space z" })
        {
            Assert.Equal(PaneState.None, Panes(Press(state, keys)));
        }
    }

    [Fact]
    public void GivenPanes_WhenSpaceZIsPressedTwice_ThenTheFocusedPaneZoomsAndLeaves()
    {
        var focused = Press(WithPanes(), "Ctrl-j");

        var zoomed = Press(focused, "Space z");
        var left = Press(zoomed, "Space z");

        Assert.Equal("description", Panes(zoomed).Zoomed);
        Assert.Null(Panes(left).Zoomed);
        Assert.Equal("description", Panes(left).Focused);
    }

    [Fact]
    public void GivenAZoomOverAnIssue_WhenEscIsPressed_ThenTheZoomLeavesBeforeTheStackPops()
    {
        var opened = _reducer.Reduce(WithPanes(), new KeyPressed("Enter")).State;
        var zoomed = Press(opened, "Space z");
        var tab = zoomed.Session.Tabs[0];
        Assert.NotEmpty(tab.Detail);

        var first = _reducer.Reduce(zoomed, new KeyPressed("Esc")).State;
        var second = _reducer.Reduce(first, new KeyPressed("Esc")).State;

        Assert.Null(Panes(first).Zoomed);
        Assert.NotEmpty(first.Session.Tabs[0].Detail);
        Assert.Empty(second.Session.Tabs[0].Detail);
    }

    [Fact]
    public void GivenAZoom_WhenCtrlHAndCtrlLArePressed_ThenTheSectionSwitches()
    {
        var zoomed = Press(WithPanes(), "Space z");

        var next = Press(zoomed, "Ctrl-l");
        var back = Press(next, "Ctrl-h");

        Assert.Equal("description", Panes(next).Zoomed);
        Assert.Equal("title", Panes(back).Zoomed);
    }

    [Fact]
    public void GivenTwoTabs_WhenOneZooms_ThenTheOtherKeepsItsOwnPanes()
    {
        var state = WithPanes();
        var first = state.Session.Tabs[0];
        var second = first with { Id = new TabId(99), Panes = PaneState.None };
        var both = state with { Session = state.Session with { Tabs = [first, second] } };

        var zoomed = Press(both, "Space z");

        Assert.NotNull(zoomed.Session.Tabs[0].Panes.Zoomed);
        Assert.Equal(PaneState.None, zoomed.Session.Tabs[1].Panes);
    }

    [Fact]
    public void GivenANavigatingFormWithPanes_WhenCtrlLIsPressed_ThenFocusMovesAndTheFieldIsNotEdited()
    {
        var form = WithForm(WithPanes());

        var moved = Press(form, "Ctrl-l");

        Assert.Equal("details", Panes(moved).Focused);
        Assert.True(moved.Session.Tabs[0].Form!.Navigating);
        Assert.Equal(0, moved.Session.Tabs[0].Form!.Focus);
    }

    [Fact]
    public void GivenANavigatingFormWithPanes_WhenCtrlJIsPressed_ThenPaneFocusMovesInsteadOfTheField()
    {
        var moved = Press(WithForm(WithPanes()), "Ctrl-j");

        Assert.Equal("description", Panes(moved).Focused);
        Assert.Equal(0, moved.Session.Tabs[0].Form!.Focus);
    }

    [Fact]
    public void GivenANavigatingFormWithoutPanes_WhenCtrlJIsPressed_ThenTheFieldStepsAsBefore()
    {
        var moved = Press(WithForm(Tabbed()), "Ctrl-j");

        Assert.Equal(1, moved.Session.Tabs[0].Form!.Focus);
    }

    [Fact]
    public void GivenANavigatingFormWithoutPanes_WhenCtrlLIsPressed_ThenItStillStartsEditing()
    {
        var moved = Press(WithForm(Tabbed()), "Ctrl-l");

        Assert.False(moved.Session.Tabs[0].Form!.Navigating);
    }

    [Theory]
    [InlineData("Ctrl-w j", "description")]
    [InlineData("Ctrl-w l", "details")]
    public void GivenANavigatingFormWithPanes_WhenACtrlWChordIsPressed_ThenPaneFocusMoves(string keys, string expected)
    {
        var moved = Press(WithForm(WithPanes()), keys);

        Assert.Equal(expected, Panes(moved).Focused);
        Assert.Equal(string.Empty, moved.PendingKeys);
        Assert.True(moved.Session.Tabs[0].Form!.Navigating);
        Assert.Equal(0, moved.Session.Tabs[0].Form!.Focus);
    }

    [Fact]
    public void GivenANavigatingFormWithPanes_WhenCtrlWHasBeenPressedAlone_ThenTheChordIsPending()
    {
        var pending = Press(WithForm(WithPanes()), "Ctrl-w");

        Assert.Equal("Ctrl-w", pending.PendingKeys);
    }

    [Theory]
    [InlineData("Ctrl-w x")]
    [InlineData("Ctrl-w Esc")]
    [InlineData("Ctrl-w i")]
    public void GivenANavigatingFormWithPanes_WhenTheSecondKeyIsNotAPaneKey_ThenTheChordEndsWithoutAnyEffect(string keys)
    {
        var moved = Press(WithForm(WithPanes()), keys);

        Assert.Equal(string.Empty, moved.PendingKeys);
        Assert.Null(Panes(moved).Focused);
        Assert.True(moved.Session.Tabs[0].Form!.Navigating);
    }

    [Fact]
    public void GivenANavigatingFormWithoutPanes_WhenCtrlWIsPressed_ThenNoChordStarts()
    {
        var moved = Press(WithForm(Tabbed()), "Ctrl-w");

        Assert.Equal(string.Empty, moved.PendingKeys);
    }

    [Fact]
    public void GivenAnEditingFormWithPanes_WhenCtrlWIsPressed_ThenNoChordStartsAndItStaysDeleteWord()
    {
        var editing = Press(WithForm(WithPanes()), "i");

        var moved = Press(editing, "Ctrl-w");

        Assert.Equal(string.Empty, moved.PendingKeys);
        Assert.False(moved.Session.Tabs[0].Form!.Navigating);
    }

    [Fact]
    public void GivenAnEditingFormWithPanes_WhenCtrlJKLArePressed_ThenPaneFocusDoesNotMove()
    {
        var editing = Press(WithForm(WithPanes()), "i");
        Assert.False(editing.Session.Tabs[0].Form!.Navigating);

        foreach (var keys in new[] { "Ctrl-j", "Ctrl-k", "Ctrl-h" })
        {
            Assert.Null(Panes(Press(editing, keys)).Focused);
        }

        var accepted = Press(editing, "Ctrl-l");

        Assert.Null(Panes(accepted).Focused);
        Assert.True(accepted.Session.Tabs[0].Form!.Navigating);
    }

    [Fact]
    public void GivenAZoomAndAnOpenPalette_WhenEscIsPressed_ThenThePaletteClosesAndTheZoomStays()
    {
        var zoomed = Press(WithPanes(), "Space z");
        var palette = Press(zoomed, ":");
        Assert.Equal(InputMode.Command, palette.Mode);

        var closed = Press(palette, "Esc");

        Assert.Equal(InputMode.Normal, closed.Mode);
        Assert.Equal("title", Panes(closed).Zoomed);
    }

    [Fact]
    public void GivenInsertModeOutsideForms_WhenCtrlJKLArePressed_ThenTheyKeepTheirPickerMeaning()
    {
        var keymap = DefaultKeymap.Load();

        Assert.Equal(ActionIds.PickerNext, keymap.Resolve(InputMode.Insert, string.Empty, "Ctrl-j").Action);
        Assert.Equal(ActionIds.PickerPrevious, keymap.Resolve(InputMode.Insert, string.Empty, "Ctrl-k").Action);
        Assert.Equal(ActionIds.FormNext, keymap.Resolve(InputMode.Insert, string.Empty, "Ctrl-l").Action);
        Assert.Equal(ActionIds.PaneFocusDown, keymap.Resolve(InputMode.Normal, string.Empty, "Ctrl-j").Action);
    }

    private static PaneState Panes(AppState state)
    {
        return state.Session.Tabs[0].Panes;
    }

    private AppState Press(AppState state, string keys)
    {
        var current = state;

        foreach (var key in keys.Split(' '))
        {
            current = _reducer.Reduce(current, new KeyPressed(key)).State;
        }

        return current;
    }

    private AppState WithPanes()
    {
        var state = Tabbed();
        var tab = state.Session.Tabs[0];

        return state with
        {
            Session = state.Session with { Tabs = [tab with { Panes = new PaneState(_grid) }] },
        };
    }

    private AppState WithForm(AppState state)
    {
        var opened = _reducer.Reduce(
            state with { Mode = InputMode.Command, PaletteText = "labels" },
            new KeyPressed("Enter")).State;
        var tab = opened.Session.Tabs[0];
        var one = FormField.Create("summary", "Summary", FormFieldKind.Text, false, []);
        var two = FormField.Create("labelsText", "Other", FormFieldKind.Text, false, []);

        return _reducer.Reduce(opened, new FormMetadataLoaded(tab.Id, tab.Form!.RequestId, [one, two])).State;
    }

    private AppState Tabbed()
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var state = AppState.Initial with
        {
            Profiles = profiles,
            Session = SiteSession.Empty(profiles.Active) with { Connection = new Connected("Me", "acc-me") },
        };
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "A");
        var opened = _reducer.Reduce(state, new ViewOpenRequested(definition)).State;
        var tab = opened.Session.Tabs[0];
        var page = new IssuePage([Issue()], null, null);

        return _reducer.Reduce(opened, new ViewPageLoaded(tab.Id, tab.RequestId, page, _start)).State;
    }

    private static IssueSummary Issue()
    {
        return new IssueSummary(
            "1",
            "A-1",
            "Issue 1",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            ["backend"],
            null,
            null,
            "A");
    }
}
