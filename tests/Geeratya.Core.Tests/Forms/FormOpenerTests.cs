using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Suggestions.Models;
using Geeratya.Jira.Transitions.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Tests.Forms;

/// <summary>
/// The keys and commands that open the edit form (<c>issue.edit</c>, <c>:priority</c>) and the people picker
/// (<c>issue.assign</c>, <c>issue.assign-me</c>), through the reducer: what opens, what a keystroke asks the network,
/// what is written on save and what goes back when Jira says no.
/// </summary>
[Trait("Os", "Independent")]
public class FormOpenerTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueAssignee _eve = new("acc-eve", "Eve Example");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenARow_WhenSpaceIEIsPressed_ThenTheEditFormOpensAndRequestsTheMetadata()
    {
        var result = Keys(Tabbed(), "Space", "i", "e");

        var form = result.State.Session.Tabs[0].Form!;
        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects));
        var target = Assert.IsType<EditFormTarget>(effect.Target);
        Assert.Equal(InputMode.Insert, result.State.Mode);
        Assert.True(form.IsLoading);
        Assert.Equal("Issue 1", target.Known[FormFieldIds.Summary].Text);
        Assert.Equal(["backend"], target.Known[FormFieldIds.Labels].Chosen);
        Assert.Equal("High", target.Known[FormFieldIds.Priority].Text);
        Assert.False(target.Known.ContainsKey(FormFieldIds.Description));
    }

    [Fact]
    public void GivenARow_WhenPriorityIsRun_ThenTheFormIsLimitedToThePriorityField()
    {
        var result = Palette(Tabbed(), "priority");

        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects));
        Assert.Equal(FormFieldIds.Priority, Assert.IsType<EditFormTarget>(effect.Target).OnlyId);
    }

    [Fact]
    public void GivenARow_WhenEstimateIsRun_ThenTheFormIsLimitedToTheEstimateNumberField()
    {
        var result = Palette(Tabbed(), "estimate");

        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects));
        var target = Assert.IsType<EditFormTarget>(effect.Target);
        Assert.True(target.EstimateOnly);
        Assert.Equal(FormFieldKind.Number, target.Only);
        Assert.Empty(target.Known);
    }

    [Fact]
    public void GivenNoSelection_WhenEstimateIsRun_ThenOnlyTheStatusLineSaysWhy()
    {
        var result = Palette(AppState.Initial, "estimate");

        Assert.Empty(result.Effects);
        Assert.Contains("No issue selected", result.State.Message);
    }

    [Fact]
    public void GivenAnEstimateTyped_WhenSavedAndRefused_ThenTheRowShowsItAtOnceAndTheOldOneComesBack()
    {
        var field = FormField.Create("customfield_10047", "Story Points", FormFieldKind.Number, false, [])
            .WithInitial(FormValue.OfText("3"));
        var loaded = LoadedForm(Palette(Tabbed(), "estimate"), field);

        var typed = Keys(loaded, "Backspace", "8");
        var saved = Press(typed.State, "Ctrl-s", _start);

        var id = saved.State.Session.Operations[0].Id;
        var request = Assert.IsType<EditRequest>(WrittenBy(saved));
        Assert.True(request.EstimateOnly);
        Assert.Equal(8, saved.State.Session.Tabs[0].Rows[0].Estimate);

        var denied = new WriteError(WriteErrorKind.Permission, 403, "no", [], []);
        var failed = _reducer.Reduce(saved.State, new WriteFailed(id, _start, denied));

        Assert.Equal(3, failed.State.Session.Tabs[0].Rows[0].Estimate);
    }

    [Fact]
    public void GivenAnEstimateEditThatIsEmptied_WhenSaved_ThenTheRowShowsNoEstimate()
    {
        var field = FormField.Create("customfield_10047", "Story Points", FormFieldKind.Number, false, [])
            .WithInitial(FormValue.OfText("3"));
        var loaded = LoadedForm(Palette(Tabbed(), "estimate"), field);

        var saved = Press(Keys(loaded, "Backspace").State, "Ctrl-s", _start);

        Assert.Null(saved.State.Session.Tabs[0].Rows[0].Estimate);
    }

    [Fact]
    public void GivenAnEstimateEdit_WhenTheReadBackRowIsChecked_ThenOnlyTheWrittenValueCountsAsShown()
    {
        var set = new FieldChange("customfield_10047", FormFieldKind.Number, FormValue.OfText("8"));
        var clear = new FieldChange("customfield_10047", FormFieldKind.Number, FormValue.Empty);
        var setRequest = new EditRequest("A-1", [set], true);
        var clearRequest = new EditRequest("A-1", [clear], true);

        Assert.Equal(["customfield_10047"], setRequest.ReadBackFieldIds);
        Assert.True(setRequest.IsShownBy(Issue(null) with { Estimate = 8 }));
        Assert.False(setRequest.IsShownBy(Issue(null) with { Estimate = 3 }));
        Assert.True(clearRequest.IsShownBy(Issue(null) with { Estimate = null }));
        Assert.False(clearRequest.IsShownBy(Issue(null) with { Estimate = 3 }));
    }

    [Fact]
    public void GivenABoardWithAnEstimationField_WhenEstimateIsRun_ThenTheTargetNamesThatField()
    {
        var result = Palette(WithBoard(Tabbed(), "customfield_10047", true), "estimate");

        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects));
        Assert.Equal("customfield_10047", Assert.IsType<EditFormTarget>(effect.Target).EstimateFieldId);
    }

    [Fact]
    public void GivenABoardReadWithoutEstimation_WhenEstimateIsRun_ThenNoFormOpensAndTheStatusSaysWhy()
    {
        var result = Palette(WithBoard(Tabbed(), null, true), "estimate");

        Assert.Empty(result.Effects);
        Assert.Contains("no story point estimation", result.State.Message);
    }

    [Fact]
    public void GivenACachedBoardWithoutEstimationInfo_WhenEstimateIsRun_ThenTheFormStillOpens()
    {
        var result = Palette(WithBoard(Tabbed(), null, false), "estimate");

        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects));
        Assert.Null(Assert.IsType<EditFormTarget>(effect.Target).EstimateFieldId);
    }

    [Fact]
    public void GivenAnEstimateThatDoesNotRead_WhenSaved_ThenNothingIsWrittenAndTheFieldShowsTheError()
    {
        var field = FormField.Create("customfield_10047", "Story Points", FormFieldKind.Number, false, [])
            .WithInitial(FormValue.OfText("3"));
        var loaded = LoadedForm(Palette(Tabbed(), "estimate"), field);

        var saved = Press(Keys(loaded, "x").State, "Ctrl-s", _start);

        Assert.Empty(saved.Effects);
        Assert.Equal(3, saved.State.Session.Tabs[0].Rows[0].Estimate);
        Assert.NotNull(saved.State.Session.Tabs[0].Form!.Fields[0].Error);
    }

    [Fact]
    public void GivenAnOpenedIssueWithATable_WhenEditIsRun_ThenTheDescriptionIsNotOfferedForEditing()
    {
        var table = new AdfTable([]);
        var document = new AdfDocument(1, [table], false);

        var result = Palette(WithDetail(Tabbed(), document), "edit");

        var target = EditTargetOf(result);
        Assert.Contains(FormFieldIds.Description, target.ReadOnlyIds!);
        Assert.False(target.Known.ContainsKey(FormFieldIds.Description));
    }

    [Fact]
    public void GivenAnOpenedIssueWithPlainText_WhenEditIsRun_ThenTheDescriptionIsOfferedAsText()
    {
        var document = AdfMarkdownParser.Parse("Hello **world**");

        var result = Palette(WithDetail(Tabbed(), document), "edit");

        var target = EditTargetOf(result);
        Assert.Equal("Hello **world**", target.Known[FormFieldIds.Description].Text);
        Assert.Empty(target.ReadOnlyIds!);
    }

    [Fact]
    public void GivenNoSelection_WhenAssignIsPressed_ThenOnlyTheStatusLineSaysWhy()
    {
        var state = AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_work) };

        var result = Keys(state, "Space", "i", "a");

        Assert.Empty(result.Effects);
        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Contains("No issue selected", result.State.Message);
    }

    [Fact]
    public void GivenARow_WhenSpaceIAIsPressed_ThenThePickerOpensAtOnceWithNoRequest()
    {
        var result = Keys(Tabbed(), "Space", "i", "a");

        var form = result.State.Session.Tabs[0].Form!;
        var field = Assert.Single(form.Fields);
        Assert.Empty(result.Effects);
        Assert.False(form.IsLoading);
        Assert.Equal("Assign A-1", form.Target.Title);
        Assert.Equal(FormFieldKind.User, field.Kind);
        Assert.Empty(field.Options);
    }

    [Fact]
    public void GivenTheCurrentAssignee_WhenThePickerOpens_ThenTheyAreNamedAndChosen()
    {
        var result = Keys(Tabbed(_eve), "Space", "i", "a");

        var field = result.State.Session.Tabs[0].Form!.Fields[0];
        Assert.Equal(["acc-eve"], field.Options.Select(option => option.Id));
        Assert.Equal(["acc-eve"], field.Value.Chosen);
    }

    [Fact]
    public void GivenThePicker_WhenTyping_ThenEachKeystrokeAsksTheSuggestionLookupForAssignableUsers()
    {
        var opened = Keys(Tabbed(), "Space", "i", "a").State;

        var first = Press(opened, "e");
        var second = Press(first.State, "v");

        var firstSearch = Assert.IsType<SearchSuggestionsEffect>(Assert.Single(first.Effects));
        var secondSearch = Assert.IsType<SearchSuggestionsEffect>(Assert.Single(second.Effects));
        Assert.Equal("e", firstSearch.Query);
        Assert.Equal("ev", secondSearch.Query);
        Assert.Equal(SuggestionKind.AssignableUsers, secondSearch.Source.Kind);
        Assert.Equal("A-1", secondSearch.Source.IssueKey);
    }

    [Fact]
    public void GivenAPersonFound_WhenChosenAndSaved_ThenTheAssignmentIsWrittenAndTheRowShowsThem()
    {
        var saved = PickEve();

        var effect = Assert.IsType<ExecuteWriteEffect>(Assert.Single(saved.Effects));
        var request = Assert.IsType<AssignRequest>(effect.Request);
        Assert.Equal(_eve, request.Assignee);
        Assert.Equal(_eve, saved.State.Session.Tabs[0].Rows[0].Assignee);
        Assert.Equal("Assigning A-1 to Eve Example", saved.State.Message);
    }

    [Fact]
    public void GivenAnAssigneeJiraRefuses_WhenTheAnswerArrives_ThenTheRowGoesBackAndTheFormShowsWhy()
    {
        var saved = PickEve();
        var id = saved.State.Session.Operations[0].Id;
        var error = new WriteError(WriteErrorKind.Validation, 400, "bad", [], ["User cannot be assigned"]);

        var failed = _reducer.Reduce(saved.State, new WriteFailed(id, _start, error));

        var tab = failed.State.Session.Tabs[0];
        Assert.Null(tab.Rows[0].Assignee);
        Assert.NotNull(tab.Form);
        Assert.NotNull(tab.Form.TopError);
        Assert.Null(tab.Form.OperationId);
    }

    [Fact]
    public void GivenTheCurrentAssignee_WhenUnassignedIsChosenAndSaved_ThenTheIssueIsUnassigned()
    {
        var opened = Keys(Tabbed(_eve), "Space", "i", "a").State;

        var cleared = Press(opened, "Backspace").State;
        var saved = Press(cleared, "Enter", _start);

        var request = Assert.IsType<AssignRequest>(WrittenBy(saved));
        Assert.Null(request.Assignee);
        Assert.Null(saved.State.Session.Tabs[0].Rows[0].Assignee);
    }

    [Theory]
    [InlineData("q")]
    [InlineData("g")]
    [InlineData(":")]
    public void GivenAnOpenFieldForm_WhenAnyKeyArrives_ThenTheFormRouteTakesItAndTheBodyNeverSeesIt(string key)
    {
        var opened = Keys(Tabbed(_eve), "Space", "i", "a").State;

        var route = InputRouting.Route(opened, key, DefaultKeymap.Load());
        var result = Press(opened, key);

        Assert.Equal(KeyRouteKind.Form, route.Kind);
        Assert.Empty(result.Effects.OfType<QuitEffect>());
        Assert.Equal(InputMode.Insert, result.State.Mode);
        Assert.NotNull(result.State.Session.Tabs[0].Form);
    }

    [Fact]
    public void GivenTheCurrentAssignee_WhenEnterIsPressedAtOnce_ThenNothingIsWrittenAndTheyStayAssigned()
    {
        var opened = Keys(Tabbed(_eve), "Space", "i", "a").State;

        var saved = Press(opened, "Enter");

        Assert.Empty(saved.Effects);
        Assert.Equal(_eve, saved.State.Session.Tabs[0].Rows[0].Assignee);
    }

    [Fact]
    public void GivenAnUnassignedIssue_WhenThePickerOpens_ThenOnlyMeIsOfferedAndNothingIsHighlighted()
    {
        var popup = Keys(Tabbed(), "Space", "i", "a").State.Session.Tabs[0].Form!.Suggest!;

        var me = Assert.Single(popup.Items);
        Assert.Equal("acc-me", me.Id);
        Assert.Equal("Me (Me)", SuggestionText.Row(me));
        Assert.Null(popup.Selected);
    }

    [Fact]
    public void GivenAnAssignedIssue_WhenThePickerOpens_ThenMeAndUnassignedAreOfferedInThatOrder()
    {
        var popup = Keys(Tabbed(_eve), "Space", "i", "a").State.Session.Tabs[0].Form!.Suggest!;

        Assert.Equal(["acc-me", string.Empty], popup.Items.Select(item => item.Id));
        Assert.Equal("Unassigned", SuggestionText.Row(popup.Items[1]));
    }

    [Fact]
    public void GivenTheQuickRows_WhenMeIsChosenAndSaved_ThenTheIssueIsAssignedToTheAccountOfTheProfile()
    {
        var opened = Keys(Tabbed(_eve), "Space", "i", "a").State;

        var chosen = Press(Press(opened, "Down").State, "Enter").State;
        var saved = Press(chosen, "Enter", _start);

        var request = Assert.IsType<AssignRequest>(WrittenBy(saved));
        Assert.Equal("acc-me", request.Assignee!.AccountId);
        Assert.Equal("Me", saved.State.Session.Tabs[0].Rows[0].Assignee!.DisplayName);
    }

    [Fact]
    public void GivenTheQuickRows_WhenUnassignedIsChosenAndSaved_ThenTheIssueIsUnassigned()
    {
        var opened = Keys(Tabbed(_eve), "Space", "i", "a").State;

        var chosen = Keys(opened, "Down", "Down", "Enter").State;
        var saved = Press(chosen, "Enter", _start);

        Assert.Null(Assert.IsType<AssignRequest>(WrittenBy(saved)).Assignee);
        Assert.Null(saved.State.Session.Tabs[0].Rows[0].Assignee);
    }

    [Fact]
    public void GivenTheQuickRows_WhenTyping_ThenTheyGiveWayToTheSearchAndReturnWhenTheFieldIsEmptyAgain()
    {
        var opened = Keys(Tabbed(_eve), "Space", "i", "a").State;

        var typed = Press(opened, "e");
        var emptied = Press(typed.State, "Backspace");

        Assert.IsType<SearchSuggestionsEffect>(Assert.Single(typed.Effects));
        Assert.Equal("e", typed.State.Session.Tabs[0].Form!.Suggest!.Query);
        Assert.IsType<CancelSuggestionsEffect>(Assert.Single(emptied.Effects));
        Assert.Equal(2, emptied.State.Session.Tabs[0].Form!.Suggest!.Items.Count);
    }

    [Fact]
    public void GivenTheCurrentAssigneeIsMe_WhenThePickerOpens_ThenOnlyUnassignedIsOffered()
    {
        var popup = Keys(Tabbed(new IssueAssignee("acc-me", "Me")), "Space", "i", "a")
            .State.Session.Tabs[0].Form!.Suggest!;

        Assert.Equal(string.Empty, Assert.Single(popup.Items).Id);
    }

    [Fact]
    public void GivenTheQuickRows_WhenBackspaceClearsTheAssignee_ThenTheRowsFollowTheNewValue()
    {
        var opened = Keys(Tabbed(_eve), "Space", "i", "a").State;

        var cleared = Press(opened, "Backspace").State;

        var popup = cleared.Session.Tabs[0].Form!.Suggest!;
        Assert.Equal(["acc-me"], popup.Items.Select(item => item.Id));
    }

    [Fact]
    public void GivenTheQuickRows_WhenEscIsPressed_ThenTheFormClosesAtOnce()
    {
        var opened = Keys(Tabbed(_eve), "Space", "i", "a").State;

        var closed = Press(opened, "Esc");

        Assert.Null(closed.State.Session.Tabs[0].Form);
    }

    [Fact]
    public void GivenAnEmptyId_WhenChosenOnAPickedField_ThenTheValueIsCleared()
    {
        var field = FormField.Create(FormFieldIds.Assignee, "Assignee", FormFieldKind.User, false, [])
            .WithInitial(FormValue.OfChosen(["acc-eve"]));

        var chosen = FormEditor.Choose(field, AssigneeQuickRows.Unassigned);

        Assert.Empty(chosen.Value.Chosen);
    }

    [Fact]
    public void GivenAnAnswerArrived_WhenEnterIsPressed_ThenThePersonIsChosen()
    {
        var typed = Press(Keys(Tabbed(), "Space", "i", "a").State, "a");
        var search = Assert.IsType<SearchSuggestionsEffect>(Assert.Single(typed.Effects));
        var found = Found(typed.State, search, "acc-eve", "Eve Example");

        var saved = Press(Press(found.State, "Enter").State, "Enter", _start);

        Assert.Equal("acc-eve", Assert.IsType<AssignRequest>(WrittenBy(saved)).Assignee!.AccountId);
    }

    [Fact]
    public void GivenAnUnassignedIssue_WhenUnassignedIsChosenAndSaved_ThenNothingIsSent()
    {
        var opened = Keys(Tabbed(), "Space", "i", "a").State;

        var saved = Press(opened, "Enter");

        Assert.Empty(saved.Effects);
        Assert.Equal("Nothing changed", saved.State.Session.Tabs[0].Form!.TopError);
    }

    [Fact]
    public void GivenThePickerWithNothingChanged_WhenEscIsPressed_ThenItClosesAndSendsNothing()
    {
        var opened = Keys(Tabbed(), "Space", "i", "a").State;

        var closed = Press(opened, "Esc");

        Assert.Empty(closed.Effects);
        Assert.Null(closed.State.Session.Tabs[0].Form);
        Assert.Equal(InputMode.Normal, closed.State.Mode);
        Assert.Empty(closed.State.Session.Operations);
    }

    [Fact]
    public void GivenARow_WhenSpaceIMIsPressed_ThenTheIssueIsAssignedToTheAccountOfTheProfile()
    {
        var result = Keys(Tabbed(), "Space", "i", "m");

        var effect = Assert.IsType<ExecuteWriteEffect>(Assert.Single(result.Effects));
        var request = Assert.IsType<AssignRequest>(effect.Request);
        Assert.Equal("acc-me", request.Assignee!.AccountId);
        Assert.Equal("Me", result.State.Session.Tabs[0].Rows[0].Assignee!.DisplayName);
    }

    [Fact]
    public void GivenARunningWriteOnTheIssue_WhenAssignMeIsPressed_ThenItIsRefusedAsBusy()
    {
        var started = Keys(Tabbed(), "Space", "i", "m").State;

        var second = Keys(started, "Space", "i", "m");

        Assert.Empty(second.Effects);
        Assert.Contains("A-1 is busy", second.State.Message);
    }

    [Fact]
    public void GivenAPriorityPicked_WhenSaved_ThenTheRowShowsItAndAFailureRestoresTheOldOne()
    {
        var field = FormField.Create(
            FormFieldIds.Priority,
            "Priority",
            FormFieldKind.Select,
            true,
            [new FieldOption("1", "Highest"), new FieldOption("2", "High")]).WithInitial(FormValue.OfChosen(["2"]));
        var loaded = LoadedForm(Palette(Tabbed(), "priority"), field);

        var saved = Press(Press(loaded, "Down").State, "Enter", _start);

        var id = saved.State.Session.Operations[0].Id;
        var request = Assert.IsType<EditRequest>(WrittenBy(saved));
        Assert.Equal("Highest", saved.State.Session.Tabs[0].Rows[0].Priority);
        Assert.Equal(["Highest"], Assert.Single(request.Changes).Display);

        var denied = new WriteError(WriteErrorKind.Permission, 403, "no", [], []);
        var failed = _reducer.Reduce(saved.State, new WriteFailed(id, _start, denied));

        Assert.Equal("High", failed.State.Session.Tabs[0].Rows[0].Priority);
        Assert.Contains("No permission to edit A-1", failed.State.Session.Tabs[0].Form!.TopError);
    }

    [Fact]
    public void GivenTheIssueChangedMeanwhile_WhenJiraAnswersConflict_ThenTheRowIsRestoredAndTheIssueReloads()
    {
        var opened = WithDetail(Tabbed(), null);
        var picked = Press(Keys(opened, "Space", "i", "a").State, "e");
        var search = Assert.IsType<SearchSuggestionsEffect>(Assert.Single(picked.Effects));
        var found = Found(picked.State, search, "acc-eve", "Eve Example");
        var saved = Press(Press(found.State, "Enter").State, "Enter", _start);
        var id = saved.State.Session.Operations[0].Id;

        var conflict = new WriteError(WriteErrorKind.Conflict, 409, "x", [], []);
        var failed = _reducer.Reduce(saved.State, new WriteFailed(id, _start, conflict));

        Assert.Null(failed.State.Session.Tabs[0].Rows[0].Assignee);
        Assert.Contains("changed meanwhile", failed.State.Session.Tabs[0].Form!.TopError);
        Assert.Contains(failed.Effects, effect => effect is LoadIssueDetailEffect);
    }

    private ReduceResult PickEve()
    {
        var opened = Keys(Tabbed(), "Space", "i", "a").State;
        var typed = Press(opened, "e");
        var search = Assert.IsType<SearchSuggestionsEffect>(Assert.Single(typed.Effects));
        var found = Found(typed.State, search, "acc-eve", "Eve Example");

        return Press(Press(found.State, "Enter").State, "Enter", _start);
    }

    private AppState LoadedForm(ReduceResult opened, FormField field)
    {
        var tab = opened.State.Session.Tabs[0];

        return _reducer.Reduce(opened.State, new FormMetadataLoaded(tab.Id, tab.Form!.RequestId, [field])).State;
    }

    private static EditFormTarget EditTargetOf(ReduceResult result)
    {
        var effect = Assert.IsType<LoadFormMetadataEffect>(Assert.Single(result.Effects));

        return Assert.IsType<EditFormTarget>(effect.Target);
    }

    private static WriteRequest WrittenBy(ReduceResult result)
    {
        return Assert.IsType<ExecuteWriteEffect>(Assert.Single(result.Effects)).Request;
    }

    private ReduceResult Found(AppState state, SearchSuggestionsEffect search, string id, string label)
    {
        var tab = state.Session.Tabs[0];
        var items = new[] { new Suggestion(id, label, null, false) };

        return _reducer.Reduce(state, new SuggestionsLoaded(tab.Id, search.RequestId, items));
    }

    private ReduceResult Keys(AppState state, params string[] keys)
    {
        var result = new ReduceResult(state, []);

        foreach (var key in keys)
        {
            result = Press(result.State, key);
        }

        return result;
    }

    private ReduceResult Press(AppState state, string key, DateTimeOffset at = default)
    {
        return _reducer.Reduce(state, new KeyPressed(key, at));
    }

    private ReduceResult Palette(AppState state, string command)
    {
        return _reducer.Reduce(state with { Mode = InputMode.Command, PaletteText = command }, new KeyPressed("Enter"));
    }

    private AppState WithDetail(AppState state, AdfDocument? description)
    {
        var tab = state.Session.Tabs[0];
        var opened = _reducer.Reduce(state, new IssueDetailOpenRequested(tab.Id, "A-1", tab.Rows[0])).State;
        var shown = opened.Session.Tabs[0].Detail[0];
        var issue = Detailed(description);

        return _reducer.Reduce(opened, new IssueDetailLoaded(tab.Id, shown.IssueRequest, issue, _start)).State;
    }

    private static AppState WithBoard(AppState state, string? estimationFieldId, bool read)
    {
        var board = new BoardConfiguration(4, "Board", BoardType.Scrum, [], "1", null, null, null)
        {
            EstimationFieldId = estimationFieldId,
            EstimationRead = read,
        };
        var tab = state.Session.Tabs[0] with { Board = board };

        return state with { Session = state.Session with { Tabs = [tab] } };
    }

    private AppState Tabbed(IssueAssignee? assignee = null)
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
        var page = new IssuePage([Issue(assignee)], null, null);

        return _reducer.Reduce(opened, new ViewPageLoaded(tab.Id, tab.RequestId, page, _start)).State;
    }

    private static IssueSummary Issue(IssueAssignee? assignee)
    {
        return new IssueSummary(
            "1",
            "A-1",
            "Issue 1",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            "High",
            assignee,
            ["backend"],
            null,
            null,
            "A")
        {
            Estimate = 3,
        };
    }

    private static IssueDetail Detailed(AdfDocument? description)
    {
        return new IssueDetail(
            "1",
            "A-1",
            "Issue 1",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            "High",
            null,
            null,
            ["backend"],
            null,
            null,
            "A",
            description,
            null,
            [],
            [],
            []);
    }
}
