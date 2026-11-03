using Geeratya.Core.CommandPalette;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.CommandPalette;

[Trait("Os", "Independent")]
public class PaletteReducerTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly KeymapTable _keymap = DefaultKeymap.Load();
    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Theory]
    [InlineData(":")]
    [InlineData("Ctrl-p")]
    public void GivenNormalMode_WhenPressingAPaletteKey_ThenCommandModeOpensEmpty(string key)
    {
        var result = Press(AppState.Initial with { PaletteText = "old", PaletteSelected = 4 }, key);

        Assert.Equal(InputMode.Command, result.State.Mode);
        Assert.Equal(string.Empty, result.State.PaletteText);
        Assert.Equal(0, result.State.PaletteSelected);
    }

    [Fact]
    public void GivenOpenPalette_WhenPressingDownAndCtrlN_ThenTheHighlightMovesAndStopsAtTheLastRow()
    {
        var state = Open();
        var count = PaletteRows.Selectable(state, _keymap).Count;

        var down = Press(state, "Down").State;
        var twice = Press(down, "Ctrl-n").State;
        var far = state;

        for (var step = 0; step < count + 5; step++)
        {
            far = Press(far, "Down").State;
        }

        Assert.Equal(1, down.PaletteSelected);
        Assert.Equal(2, twice.PaletteSelected);
        Assert.Equal(count - 1, far.PaletteSelected);
    }

    [Fact]
    public void GivenOpenPalette_WhenPressingUpAndCtrlP_ThenTheHighlightMovesUpAndPaletteStaysOpen()
    {
        var state = Press(Press(Open(), "Down").State, "Down").State;

        var up = Press(state, "Up").State;
        var ctrlP = Press(up, "Ctrl-p").State;
        var top = Press(ctrlP, "Up").State;

        Assert.Equal(1, up.PaletteSelected);
        Assert.Equal(0, ctrlP.PaletteSelected);
        Assert.Equal(0, top.PaletteSelected);
        Assert.Equal(InputMode.Command, top.Mode);
    }

    [Fact]
    public void GivenAMovedHighlight_WhenTypingOrErasing_ThenTheHighlightReturnsToTheFirstRow()
    {
        var moved = Press(Open(), "Down").State;

        var typed = Press(moved, "c").State;
        var erased = Press(Press(typed, "Down").State, "Backspace").State;

        Assert.Equal(0, typed.PaletteSelected);
        Assert.Equal(0, erased.PaletteSelected);
        Assert.Equal(string.Empty, erased.PaletteText);
    }

    [Fact]
    public void GivenAFilter_WhenPressingEnterOnAnAction_ThenThePaletteClosesAndTheActionRuns()
    {
        var result = Press(Typed("hide help"), "Enter");

        Assert.Equal(InputMode.Help, result.State.Mode);
        Assert.Equal(string.Empty, result.State.PaletteText);
    }

    [Fact]
    public void GivenALoadedIssue_WhenRunningDeleteFromThePalette_ThenTheSameGuardedPathAsTheKeyStarts()
    {
        var viaPalette = Press(Typed("Delete the issue", WithRow()), "Enter");
        var viaKey = _reducer.Reduce(WithRow(), new ActionRequested(ActionIds.IssueDelete, 1, _now));

        Assert.Equal(InputMode.Normal, viaPalette.State.Mode);
        Assert.Equal(viaKey.State.Message, viaPalette.State.Message);
        Assert.Equal(viaKey.Effects.Select(effect => effect.GetType()), viaPalette.Effects.Select(effect => effect.GetType()));
        Assert.Single(viaPalette.Effects);
    }

    [Fact]
    public void GivenAnActionThatNeedsAForm_WhenRunningItFromThePalette_ThenTheFormOpensAfterThePaletteClosed()
    {
        var result = Press(Typed("Create an issue", WithRow()), "Enter");

        Assert.NotEqual(InputMode.Command, result.State.Mode);
        Assert.Equal(string.Empty, result.State.PaletteText);
    }

    [Fact]
    public void GivenTwoMatches_WhenMovingDownAndPressingEnter_ThenTheSecondRowRunsNotTheFirst()
    {
        var state = Typed("the tab", WithRow());
        var rows = PaletteRows.Selectable(state, _keymap);

        var result = Press(Press(state, "Down").State, "Enter");

        Assert.Equal(ActionIds.TabClose, rows[0].Id);
        Assert.Equal(ActionIds.TabNew, rows[1].Id);
        Assert.NotNull(result.State.Picker);
        Assert.Single(result.State.Session.Tabs);
    }

    [Fact]
    public void GivenAPaletteOnlyRow_WhenPressingEnter_ThenItRunsAsItsCommandLine()
    {
        var typed = Typed("labels");

        var result = Press(typed, "Enter");

        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Contains("No issue selected", result.State.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenATypedCommandWithArguments_WhenPressingEnter_ThenItRunsAsACommandLineNotARow()
    {
        var result = Press(Typed("jql project = A", Signed()), "Enter");

        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Single(result.State.Session.Tabs);
    }

    [Fact]
    public void GivenTextThatMatchesNothing_WhenPressingEnter_ThenTheCommandIsUnknown()
    {
        var result = Press(Typed("zzqx"), "Enter");

        Assert.Equal("Unknown command: zzqx", result.State.Message);
    }

    [Fact]
    public void GivenAnEmptyFilter_WhenPressingEnter_ThenTheHighlightedFirstRowRuns()
    {
        var first = PaletteRows.Selectable(Open(), _keymap)[0];

        var result = Press(Open(), "Enter");

        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.False(string.IsNullOrEmpty(first.Id));
    }

    [Fact]
    public void GivenOpenPalette_WhenPressingEsc_ThenItClosesAndNothingRuns()
    {
        var result = Press(Press(Typed("quit"), "Down").State, "Esc");

        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenOpenPalette_WhenClickingARow_ThenThatRowRunsNotTheHighlightedOne()
    {
        var state = Typed("hide help");
        var clicked = new OverlayRowClicked(MouseTarget.PaletteRow, 0, ActionIds.FormNext, _now);

        var result = _reducer.Reduce(state, clicked);

        Assert.Equal(InputMode.Help, result.State.Mode);
    }

    [Fact]
    public void GivenOpenPalette_WhenWheelingOverTheRows_ThenTheHighlightMovesThreeAndNothingRuns()
    {
        var state = Open();
        var down = new OverlayRowClicked(MouseTarget.PaletteRow, 0, ActionIds.PickerNext, _now);
        var up = new OverlayRowClicked(MouseTarget.PaletteRow, 0, ActionIds.PickerPrevious, _now);

        var moved = _reducer.Reduce(state, down);
        var back = _reducer.Reduce(moved.State, up);

        Assert.Equal(3, moved.State.PaletteSelected);
        Assert.Equal(0, back.State.PaletteSelected);
        Assert.Equal(InputMode.Command, back.State.Mode);
        Assert.Empty(moved.Effects);
    }

    [Fact]
    public void GivenPasteIntoThePalette_WhenFiltering_ThenTheHighlightResets()
    {
        var moved = Press(Open(), "Down").State;

        var pasted = _reducer.Reduce(moved, new TextPasted("quit")).State;

        Assert.Equal("quit", pasted.PaletteText);
        Assert.Equal(0, pasted.PaletteSelected);
    }

    [Fact]
    public void GivenTheDefaultKeymap_WhenCheckingGroups_ThenEveryBoundNormalActionHasAGroup()
    {
        var bound = _keymap.Bindings.Where(binding => binding.Mode == InputMode.Normal).Select(binding => binding.Action);

        Assert.All(bound.Distinct(), action => Assert.NotNull(ActionCatalog.GroupOf(action)));
    }

    [Fact]
    public void GivenTheCatalog_WhenCheckingGroups_ThenEveryListedActionHasAGroupOfTheFixedList()
    {
        var listed = ActionCatalog.All.Where(definition => !ActionCatalog.IsModalOnly(definition.Id));

        Assert.All(listed, definition => Assert.Contains(ActionCatalog.GroupOf(definition.Id), ActionCatalog.Groups));
    }

    private ReduceResult Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key, _now));
    }

    private AppState Open(AppState? from = null)
    {
        return Press(from ?? AppState.Initial, ":").State;
    }

    private AppState Typed(string text, AppState? from = null)
    {
        var state = Open(from);

        foreach (var character in text)
        {
            state = Press(state, character == ' ' ? "Space" : character.ToString()).State;
        }

        return state;
    }

    private static AppState Signed()
    {
        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_work), Session = SiteSession.Empty(_work) };
    }

    private AppState WithRow()
    {
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "View");
        var sized = _reducer.Reduce(Signed(), new ViewportChanged(20, 80)).State;
        var opened = _reducer.Reduce(sized, new ViewOpenRequested(definition)).State;
        var tab = opened.Session.Tabs.Single();
        var issue = new IssueSummary(
            "1",
            "A-1",
            "Summary A-1",
            new IssueStatus("1", "Status", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");

        return _reducer.Reduce(opened, new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage([issue], null, 1), _now))
            .State;
    }
}
