using Geeratya.Core.Boards;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transitions.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Tests.Forms;

[Trait("Os", "Independent")]
public class TransitionFlowTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueStatus _progress = new("2", "In Progress", "indeterminate");
    private static readonly IssueStatus _done = new("3", "Done", "done");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenARow_WhenTheStatusKeysAreTyped_ThenThePickerOpensAndRequestsTheTransitions()
    {
        var result = Keys(Tabbed(ViewLayout.List), "Space", "i", "s");

        var form = result.State.Session.Tabs[0].Form!;
        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects));
        Assert.Equal(InputMode.Insert, result.State.Mode);
        Assert.IsType<TransitionPickTarget>(form.Target);
        Assert.Equal("A-1", effect.Target.IssueKey);
    }

    [Fact]
    public void GivenAPlainTransition_WhenPickedInTheForm_ThenTheCardMovesAtOnceAndTheFormCloses()
    {
        var opened = Picker(Tabbed(ViewLayout.List), Transition("21", _progress));

        var result = Keys(opened, "Enter");

        var write = Assert.IsType<ExecuteWriteEffect>(Assert.Single(result.Effects));
        var request = Assert.IsType<TransitionRequest>(write.Request);
        Assert.Equal("21", request.Choice.Id);
        Assert.Equal("In Progress", result.State.Session.Tabs[0].Rows[0].Status.Name);
        Assert.Null(result.State.Session.Tabs[0].Form);
        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Equal("Moving A-1 to In Progress", result.State.Message);
    }

    [Fact]
    public void GivenATransitionToADoneStatus_WhenPicked_ThenItAsksFirstAndNothingIsSentUntilYes()
    {
        var opened = Picker(Tabbed(ViewLayout.List), Transition("41", _done));

        var asked = Keys(opened, "Enter");

        Assert.Empty(asked.Effects);
        Assert.Equal(InputMode.Confirm, asked.State.Mode);
        Assert.Contains("Move A-1 to Done?", asked.State.Confirm!.Message);
        Assert.Contains("cannot undo", asked.State.Confirm.Message);
        Assert.Equal("To Do", asked.State.Session.Tabs[0].Rows[0].Status.Name);

        var confirmed = Keys(asked.State, "y");

        Assert.IsType<ExecuteWriteEffect>(Assert.Single(confirmed.Effects));
        Assert.Equal("Done", confirmed.State.Session.Tabs[0].Rows[0].Status.Name);
    }

    [Fact]
    public void GivenTheConfirmForADoneMove_WhenTheUserCancels_ThenNothingIsSentAndTheRowIsUntouched()
    {
        var opened = Picker(Tabbed(ViewLayout.List), Transition("41", _done));
        var asked = Keys(opened, "Enter");

        var declined = Keys(asked.State, "n");

        Assert.Empty(declined.Effects);
        Assert.Empty(declined.State.Session.Operations);
        Assert.Equal("To Do", declined.State.Session.Tabs[0].Rows[0].Status.Name);
        Assert.Equal(InputMode.Normal, declined.State.Mode);
    }

    [Fact]
    public void GivenTheIssueIsBusyWhenTheConfirmIsAnswered_ThenTheWriteIsRefusedAtThatMoment()
    {
        var opened = Picker(Tabbed(ViewLayout.List), Transition("41", _done));
        var asked = Keys(opened, "Enter").State;
        var busy = _reducer.Reduce(asked, new WriteRequested(
            asked.Session.Tabs[0].Id,
            new AssignRequest("A-1", null),
            _start)).State;
        var again = busy with { Mode = InputMode.Confirm, Confirm = asked.Confirm };

        var answered = Keys(again, "y");

        Assert.Empty(answered.Effects);
        Assert.Contains("busy", answered.State.Message);
        Assert.Equal("To Do", answered.State.Session.Tabs[0].Rows[0].Status.Name);
    }

    [Fact]
    public void GivenATransitionWithARequiredField_WhenPicked_ThenTheFieldFormOpensAndNothingIsWritten()
    {
        var resolution = new FieldMeta("resolution", "Resolution", true, "resolution", false, [new("10000", "Done")]);
        var opened = Picker(Tabbed(ViewLayout.List), Transition("41", _done, resolution));

        var next = Keys(opened, "Enter");

        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(next.Effects));
        var target = Assert.IsType<TransitionFormTarget>(effect.Target);
        Assert.Equal("41", target.Choice.Id);
        Assert.Equal("To Do", next.State.Session.Tabs[0].Rows[0].Status.Name);
        Assert.Empty(next.State.Session.Operations);
    }

    [Fact]
    public void GivenTheTransitionFormWithAMissingRequiredField_WhenSubmitted_ThenItStaysOpenAndSendsNothing()
    {
        var form = TransitionForm(Tabbed(ViewLayout.List), Required());

        var result = Keys(form, "Ctrl-s");

        Assert.Empty(result.Effects);
        Assert.NotNull(result.State.Session.Tabs[0].Form);
        Assert.NotNull(result.State.Session.Tabs[0].Form!.Fields[0].Error);
    }

    [Fact]
    public void GivenTheTransitionFormFilled_WhenSubmitted_ThenTheFieldsRideWithTheTransitionAndNoSecondConfirmAsks()
    {
        var form = TransitionForm(Tabbed(ViewLayout.List), Required());

        var chosen = Keys(Keys(form, "Down").State, "Enter");
        var result = chosen.Effects.Count == 0 ? Keys(chosen.State, "Ctrl-s") : chosen;

        var write = Assert.IsType<ExecuteWriteEffect>(Assert.Single(result.Effects));
        var request = Assert.IsType<TransitionRequest>(write.Request);
        Assert.Equal("resolution", Assert.Single(request.Changes).FieldId);
        Assert.Equal(InputMode.Insert, result.State.Mode);
    }

    [Fact]
    public void GivenAJiraRefusalOfTheTransition_WhenTheWriteFails_ThenTheCardGoesBackAndTheStatusLineSaysWhy()
    {
        var sent = Keys(Picker(Tabbed(ViewLayout.List), Transition("21", _progress)), "Enter").State;
        var operation = Assert.Single(sent.Session.Operations);
        var error = new WriteError(WriteErrorKind.Permission, 403, "You lack the permission", [], []);

        var failed = _reducer.Reduce(sent, new WriteFailed(operation.Id, _start, error)).State;

        Assert.Equal("To Do", failed.Session.Tabs[0].Rows[0].Status.Name);
        Assert.StartsWith("No permission to transition A-1", failed.Message);
    }

    [Fact]
    public void GivenAColumnMoveThatJiraRefuses_WhenTheWriteFails_ThenTheCardAndTheSelectionGoBackToTheFirstColumn()
    {
        var moved = LoadedPick(Tabbed(ViewLayout.Columns), "Alt-l", Transition("21", _progress));
        var operation = Assert.Single(moved.State.Session.Operations);
        var error = new WriteError(WriteErrorKind.Permission, 403, "You lack the permission", [], []);

        var failed = _reducer.Reduce(moved.State, new WriteFailed(operation.Id, _start, error)).State;

        var tab = failed.Session.Tabs[0];
        var columns = ColumnsNavigation.ColumnsOf(tab);
        Assert.Equal("To Do", tab.Rows[0].Status.Name);
        Assert.Equal(0, BoardMotion.Resolve(columns, tab.Cursor, tab.SelectedKey, 10).Column);
        Assert.Equal("A-1", ColumnsNavigation.SelectedIssue(tab)!.Key);
    }

    [Fact]
    public void GivenAnUnknownOutcome_WhenTheIssueStillShowsTheOldStatus_ThenTheMoveIsRolledBackAsNotApplied()
    {
        var sent = Keys(Picker(Tabbed(ViewLayout.List), Transition("21", _progress)), "Enter").State;
        var operation = Assert.Single(sent.Session.Operations);
        var old = sent.Session.Tabs[0].Rows[0] with { Status = _todo };

        var verified = _reducer.Reduce(sent, new WriteVerified(operation.Id, _start, VerifyOutcome.NotApplied, [old]));

        Assert.Equal("To Do", verified.State.Session.Tabs[0].Rows[0].Status.Name);
        Assert.Equal("A-1: the move was not applied", verified.State.Message);
    }

    [Fact]
    public void GivenThePaletteCommands_WhenStatusNextAndPrevAreRun_ThenTheyMoveTheCardLikeTheAltKeys()
    {
        var tabbed = Tabbed(ViewLayout.Columns);

        var next = Palette(tabbed, "status next");
        var previous = Palette(tabbed, "status prev");

        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(next.Effects));
        Assert.Equal("In Progress", Assert.IsType<TransitionPickTarget>(effect.Target).ColumnName);
        Assert.Empty(previous.Effects);
        Assert.Contains("already in the first column", previous.State.Message);
    }

    [Fact]
    public void GivenAScreenOfFieldsTheFormCannotEdit_WhenTheChoiceIsMade_ThenItNeedsNoFormAndAsksFirst()
    {
        var comment = new FieldMeta("comment", "Comment", false, "comment", false, []);
        var screen = new Transition("31", "Start", _progress, true, true, [comment]);

        var choice = TransitionChoice.Of(screen);

        Assert.False(choice.NeedsForm);
        Assert.True(choice.AsksFirst);
    }

    [Fact]
    public void GivenARequiredFieldTheFormCannotEdit_WhenTheChoiceIsMade_ThenItStillNeedsTheForm()
    {
        var user = new FieldMeta("customfield_1", "Reviewer", true, "user", false, []);

        var choice = TransitionChoice.Of(new Transition("31", "Start", _progress, true, true, [user]));

        Assert.True(choice.NeedsForm);
    }

    [Fact]
    public void GivenAnUnknownOutcome_WhenTheIssueShowsTheTarget_ThenTheMoveCountsAsDone()
    {
        var sent = Keys(Picker(Tabbed(ViewLayout.List), Transition("21", _progress)), "Enter").State;
        var operation = Assert.Single(sent.Session.Operations);
        var row = sent.Session.Tabs[0].Rows[0];

        var verified = _reducer.Reduce(sent, new WriteVerified(operation.Id, _start, VerifyOutcome.Applied, [row]));

        Assert.Equal("Moved A-1 to In Progress", verified.State.Message);
        Assert.Empty(verified.State.Session.Operations);
    }

    [Fact]
    public void GivenTheColumnsLayout_WhenAltLIsPressed_ThenTheNextColumnsTransitionIsLookedUpWithoutShowingAForm()
    {
        var result = Keys(Tabbed(ViewLayout.Columns), "Alt-l");

        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects));
        var target = Assert.IsType<TransitionPickTarget>(effect.Target);
        Assert.Equal("In Progress", target.ColumnName);
        Assert.Equal(["2"], target.ColumnStatusIds);
        Assert.False(target.ShowsWhileLoading);
    }

    [Fact]
    public void GivenExactlyOneTransitionIntoTheColumn_WhenTheMetadataLoads_ThenTheCardMovesAndSelectionFollowsIt()
    {
        var tabbed = Tabbed(ViewLayout.Columns);
        var state = LoadedPick(tabbed, "Alt-l", Transition("21", _progress), Transition("41", _done));

        var write = Assert.IsType<ExecuteWriteEffect>(Assert.Single(state.Effects));
        var tab = state.State.Session.Tabs[0];
        var request = Assert.IsType<TransitionRequest>(write.Request);
        Assert.Equal("21", request.Choice.Id);
        Assert.Null(tab.Form);
        Assert.Equal("In Progress", tab.Rows[0].Status.Name);
        Assert.Equal("A-1", ColumnsNavigation.SelectedIssue(tab)!.Key);
        Assert.Equal("In Progress", ColumnsNavigation.ColumnsOf(tab)[1].Name);
        Assert.Equal(1, BoardMotion.Resolve(ColumnsNavigation.ColumnsOf(tab), tab.Cursor, tab.SelectedKey, 10).Column);
    }

    [Fact]
    public void GivenExactlyOneTransitionIntoTheColumn_WhenTheCardIsDragged_ThenItIsSentAtOnceLikeAltL()
    {
        var tabbed = _reducer.Reduce(Tabbed(ViewLayout.Columns), new ViewportChanged(25, 90)).State;
        var dragged = _reducer.Reduce(tabbed, new CardDragged(5, 4, 45, 90));
        var keyed = Keys(tabbed, "Alt-l");

        var state = Loaded(dragged.State, [Transition("21", _progress), Transition("41", _done)]);

        var write = Assert.IsType<ExecuteWriteEffect>(Assert.Single(state.Effects));
        var request = Assert.IsType<TransitionRequest>(write.Request);
        Assert.Equal("21", request.Choice.Id);
        Assert.Null(state.State.Session.Tabs[0].Form);
        Assert.Equal("In Progress", state.State.Session.Tabs[0].Rows[0].Status.Name);
        var keyedWrite = Loaded(keyed.State, [Transition("21", _progress)]);
        var keyedRequest = Assert.IsType<ExecuteWriteEffect>(Assert.Single(keyedWrite.Effects)).Request;
        Assert.Equal(keyedRequest, write.Request);
    }

    [Fact]
    public void GivenABoardCursor_WhenACardMoves_ThenTheCursorIsKeptAndFollowsTheCardToItsColumn()
    {
        var tabbed = Keys(Tabbed(ViewLayout.Columns), "j").State;

        var state = LoadedPick(tabbed, "Alt-l", Transition("21", _progress));

        var tab = state.State.Session.Tabs[0];
        Assert.NotNull(tab.Cursor);
        Assert.Equal(1, tab.Cursor.Column);
        Assert.Equal(0, tab.Cursor.Card);
        Assert.Equal("A-1", tab.SelectedKey);
    }

    [Fact]
    public void GivenAMovedCard_WhenTheMoveFails_ThenTheCursorFollowsTheCardBackToItsColumn()
    {
        var tabbed = Keys(Tabbed(ViewLayout.Columns), "j").State;
        var moved = LoadedPick(tabbed, "Alt-l", Transition("21", _progress)).State;
        var id = moved.Session.Operations[0].Id;
        var denied = new WriteError(WriteErrorKind.Permission, 403, "no", [], []);

        var failed = _reducer.Reduce(moved, new WriteFailed(id, _start, denied)).State;

        var tab = failed.Session.Tabs[0];
        Assert.Equal("To Do", tab.Rows[0].Status.Name);
        Assert.Equal(0, tab.Cursor!.Column);
        Assert.Equal(0, tab.Cursor.Card);
    }

    [Fact]
    public void GivenTheSelectionLeftTheCard_WhenTheMoveFails_ThenTheCursorStays()
    {
        var tabbed = Keys(Tabbed(ViewLayout.Columns), "j").State;
        var moved = LoadedPick(tabbed, "Alt-l", Transition("21", _progress)).State;
        var id = moved.Session.Operations[0].Id;
        var elsewhere = moved.Session.Tabs[0] with { SelectedKey = "B-9" };
        var denied = new WriteError(WriteErrorKind.Permission, 403, "no", [], []);

        var failed = _reducer.Reduce(Replace(moved, elsewhere), new WriteFailed(id, _start, denied)).State;

        Assert.Equal(1, failed.Session.Tabs[0].Cursor!.Column);
    }

    [Fact]
    public void GivenTwoTransitionsIntoTheColumn_WhenTheMetadataLoads_ThenThePickerShowsOnlyThose()
    {
        var other = new IssueStatus("2", "In Progress", "indeterminate");
        var state = LoadedPick(
            Tabbed(ViewLayout.Columns),
            "Alt-l",
            Transition("21", _progress),
            Transition("22", other),
            Transition("41", _done));

        var form = state.State.Session.Tabs[0].Form!;
        Assert.Empty(state.Effects);
        Assert.Equal(["21", "22"], form.Fields[0].Options.Select(option => option.Id));
    }

    [Fact]
    public void GivenNoTransitionIntoTheColumn_WhenTheMetadataLoads_ThenThePickerListsEveryTransitionAndSaysSo()
    {
        var state = LoadedPick(Tabbed(ViewLayout.Columns), "Alt-l", Transition("41", _done));

        var form = state.State.Session.Tabs[0].Form!;
        Assert.Empty(state.Effects);
        Assert.Equal(["41"], form.Fields[0].Options.Select(option => option.Id));
        Assert.Contains("No transition leads to In Progress", form.Fields[0].Label);
    }

    [Fact]
    public void GivenAColumnMoveToDone_WhenTheOnlyTransitionIsFound_ThenItAsksBeforeSending()
    {
        var state = LoadedPick(Tabbed(ViewLayout.Columns, _progress), "Alt-l", Transition("41", _done));

        Assert.Empty(state.Effects);
        Assert.Equal(InputMode.Confirm, state.State.Mode);
    }

    [Fact]
    public void GivenTheLastColumn_WhenAltLIsPressed_ThenNothingHappensAndTheStatusLineSaysWhy()
    {
        var result = Keys(Tabbed(ViewLayout.Columns, _done), "Alt-l");

        Assert.Empty(result.Effects);
        Assert.Contains("already in the last column", result.State.Message);
    }

    [Fact]
    public void GivenTheFirstColumn_WhenAltHIsPressed_ThenNothingHappens()
    {
        var result = Keys(Tabbed(ViewLayout.Columns), "Alt-h");

        Assert.Empty(result.Effects);
        Assert.Contains("already in the first column", result.State.Message);
    }

    [Fact]
    public void GivenAHiddenBacklogColumn_WhenAltHIsPressedOnTheFirstShownColumn_ThenNoMoveToBacklogOpens()
    {
        var result = Keys(Tabbed(ViewLayout.Columns, backlogFirst: true), "Alt-h");

        Assert.Empty(result.Effects);
        Assert.Contains("already in the first column", result.State.Message);
    }

    [Fact]
    public void GivenTheListLayout_WhenAltLIsPressed_ThenTheStatusLineNamesTheColumnsLayout()
    {
        var result = Keys(Tabbed(ViewLayout.List), "Alt-l");

        Assert.Empty(result.Effects);
        Assert.Contains("columns layout", result.State.Message);
    }

    [Fact]
    public void GivenACardInNoColumn_WhenAltLIsPressed_ThenTheStatusLineSaysItIsUnmapped()
    {
        var result = Keys(Tabbed(ViewLayout.Columns, new IssueStatus("9", "Odd", "new")), "Alt-l");

        Assert.Empty(result.Effects);
        Assert.Contains("in no board column", result.State.Message);
    }

    [Fact]
    public void GivenNoTransitionIsAvailable_WhenThePickerLoads_ThenTheFormSaysSoAndCannotSubmit()
    {
        var opened = Picker(Tabbed(ViewLayout.List));

        var result = Keys(opened, "Enter");

        Assert.Empty(result.Effects);
        Assert.Equal("No transition is available for A-1", result.State.Session.Tabs[0].Form!.TopError);
    }

    private ReduceResult Keys(AppState state, params string[] keys)
    {
        var current = new ReduceResult(state, []);

        foreach (var key in keys)
        {
            current = _reducer.Reduce(current.State, new KeyPressed(key, _start));
        }

        return current;
    }

    private ReduceResult Palette(AppState state, string command)
    {
        var current = state with { Mode = InputMode.Command, PaletteText = command };

        return _reducer.Reduce(current, new KeyPressed("Enter", _start));
    }

    private AppState Picker(AppState tabbed, params Transition[] transitions)
    {
        var opened = Keys(tabbed, "Space", "i", "s").State;

        return Loaded(opened, transitions).State;
    }

    private ReduceResult LoadedPick(AppState tabbed, string key, params Transition[] transitions)
    {
        return Loaded(Keys(tabbed, key).State, transitions);
    }

    private ReduceResult Loaded(AppState opened, Transition[] transitions)
    {
        var tab = opened.Session.Tabs[0];
        var target = ((TransitionPickTarget)tab.Form!.Target).With(transitions);
        var loaded = new FormMetadataLoaded(tab.Id, tab.Form.RequestId, target.Fields(), target, _start);

        return _reducer.Reduce(opened, loaded);
    }

    private AppState TransitionForm(AppState tabbed, Transition transition)
    {
        var choice = TransitionChoice.Of(transition);
        var tab = tabbed.Session.Tabs[0];
        var opened = _reducer.Reduce(tabbed, new OpenFormRequested(tab.Id, new TransitionFormTarget("A-1", choice)));
        var form = opened.State.Session.Tabs[0].Form!;
        var fields = FormFieldMapper.FromTransition(transition.Fields);
        var loaded = new FormMetadataLoaded(tab.Id, form.RequestId, fields);

        return _reducer.Reduce(opened.State, loaded).State;
    }

    private static Transition Required()
    {
        var resolution = new FieldMeta("resolution", "Resolution", true, "resolution", false, [new("10000", "Done")]);

        return Transition("41", _done, resolution);
    }

    private static Transition Transition(string id, IssueStatus target, params FieldMeta[] fields)
    {
        return new Transition(id, target.Name, target, fields.Length > 0, true, fields);
    }

    private AppState Tabbed(ViewLayout layout, IssueStatus? status = null, bool backlogFirst = false)
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var state = AppState.Initial with
        {
            Profiles = profiles,
            Session = SiteSession.Empty(profiles.Active) with { Connection = new Connected("Me", "acc-me") },
        };
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", layout, "A");
        var opened = _reducer.Reduce(state, new ViewOpenRequested(definition)).State;
        var tab = opened.Session.Tabs[0];
        var page = new IssuePage([Issue("A-1", status ?? _todo)], null, null);
        var loaded = _reducer.Reduce(opened, new ViewPageLoaded(tab.Id, tab.RequestId, page, _start)).State;
        var board = new BoardConfiguration(
            1,
            "Board",
            BoardType.Kanban,
            backlogFirst
                ? [new("Backlog", ["0"]), new("To Do", ["1"]), new("In Progress", ["2"]), new("Done", ["3"])]
                : [new("To Do", ["1"]), new("In Progress", ["2"]), new("Done", ["3"])],
            "10",
            null,
            null,
            null);
        var withBoard = loaded.Session.Tabs[0] with { Board = board, SelectedKey = "A-1" };

        return Replace(loaded, withBoard);
    }

    private static AppState Replace(AppState state, TabState tab)
    {
        return state with { Session = state.Session with { Tabs = [tab] } };
    }

    private static IssueSummary Issue(string key, IssueStatus status)
    {
        return new IssueSummary(
            "1",
            key,
            "Issue 1",
            status,
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
    }
}
