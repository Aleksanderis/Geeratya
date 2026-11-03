using Geeratya.Core.Backlog;
using Geeratya.Core.Keymap;
using Geeratya.Core.Menus;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Tests.Backlog;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Tests.Sprints;

/// <summary>Create and edit a sprint through the keymap, the form and the write pipeline (#185).</summary>
[Trait("Os", "Independent")]
public class SprintFormTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenAScrumBacklog_WhenTheCreateKeyIsPressed_ThenAnEmptyFormOpensAtOnceWithoutFetching()
    {
        var result = Press(Backlog(), "Space", "s", "n");

        var form = Active(result.State).Form!;
        Assert.Empty(result.Effects);
        Assert.False(form.IsLoading);
        Assert.Equal(InputMode.Insert, result.State.Mode);
        Assert.Equal("Create sprint", form.Target.Title);
        Assert.Equal(["name", "goal", "startDate", "endDate"], form.Fields.Select(field => field.Id));
        Assert.True(form.Fields[0].Required);
        Assert.All(form.Fields, field => Assert.True(field.Value.IsEmpty));
    }

    [Theory]
    [InlineData("n")]
    [InlineData("e")]
    public void GivenAJqlTab_WhenASprintKeyIsPressed_ThenNoFormOpensAndTheStatusSaysWhy(string key)
    {
        var result = Press(Jql(), "Space", "s", key);

        Assert.Null(Active(result.State).Form);
        Assert.Equal(SprintForms.NotScrumText, result.State.Message);
    }

    [Fact]
    public void GivenTheCursorOnAnActiveSprint_WhenTheEditKeyIsPressed_ThenTheFormHoldsItsValues()
    {
        var result = Press(Backlog(), "Space", "s", "e");

        var form = Active(result.State).Form!;
        Assert.Equal("Edit sprint Sprint 1", form.Target.Title);
        Assert.Equal("Sprint 1", form.Fields[0].Value.Text);
        Assert.Equal("Ship the login", form.Fields[1].Value.Text);
        Assert.Equal(SprintFormFields.Local(BacklogFixtures.Active.StartDate), form.Fields[2].Value.Text);
        Assert.Equal(SprintFormFields.Local(BacklogFixtures.Active.EndDate), form.Fields[3].Value.Text);
        Assert.False(form.IsDirty);
    }

    [Fact]
    public void GivenTheCursorMovedOntoAnEmptySprint_WhenTheEditKeyIsPressed_ThenItsFormOpens()
    {
        var onEmpty = Press(Backlog(), "j", "j", "j", "j");

        var result = Press(onEmpty.State, "Space", "s", "e");

        Assert.Equal("Edit sprint Sprint 3", Active(result.State).Form!.Target.Title);
    }

    [Theory]
    [InlineData("Enter")]
    [InlineData("Space i e")]
    [InlineData("Space i d")]
    [InlineData("Space i s")]
    [InlineData("v")]
    [InlineData("Alt-m")]
    [InlineData("z a")]
    public void GivenTheCursorOnAnEmptySprint_WhenAKeyWouldActOnTheRowBehindIt_ThenItIsRefused(string keys)
    {
        var onEmpty = Press(Backlog(), "j", "j", "j", "j");
        Assert.NotNull(Active(onEmpty.State).FocusedSprintId);

        var result = Press(onEmpty.State, keys.Split(' '));

        Assert.Empty(result.Effects);
        Assert.Null(Active(result.State).Form);
        Assert.Empty(Active(result.State).Detail);
        Assert.Equal(EmptySprintFocus.Refusal, result.State.Message);
    }

    [Fact]
    public void GivenTheCursorOnAnEmptySprint_WhenTheContextMenuOpens_ThenNoIssueMenuShows()
    {
        var onEmpty = Press(Backlog(), "j", "j", "j", "j");

        Assert.Null(ContextMenus.For(onEmpty.State));
    }

    [Fact]
    public void GivenTheCursorOnAnEmptySprint_WhenTheRowsAreReloaded_ThenTheCursorStaysOnIt()
    {
        var onEmpty = Press(Backlog(), "j", "j", "j", "j").State;
        var tab = Active(onEmpty);

        Sprint[] sprints = [BacklogFixtures.Active, BacklogFixtures.Future, BacklogFixtures.EmptyFuture];
        var reloaded = _reducer.Reduce(onEmpty, new SprintsLoaded(tab.Id, tab.RequestId, sprints)).State;

        Assert.Equal(BacklogFixtures.EmptyFutureId, Active(reloaded).FocusedSprintId);
    }

    [Fact]
    public void GivenAnEditThatEmptiesTheEnd_WhenSubmitted_ThenTheEditAsksToClearTheEndAndKeepsTheStart()
    {
        var state = Press(Backlog(), "Space", "s", "e").State;
        var emptied = Type(state, ["Enter", "Enter", "Enter", "Enter", .. Enumerable.Repeat("Backspace", 16)]);

        var submitted = Press(emptied, "Ctrl-s");

        var request = Assert.IsType<EditSprintRequest>(
            Assert.IsType<ExecuteWriteEffect>(Assert.Single(submitted.Effects)).Request);
        Assert.True(request.Edit.ClearEndDate);
        Assert.False(request.Edit.ClearStartDate);
        Assert.Null(request.Edit.EndDate);
        Assert.Equal(BacklogFixtures.Active.StartDate, request.Edit.StartDate);
    }

    [Fact]
    public void GivenTheCursorInTheBacklogSection_WhenTheEditKeyIsPressed_ThenNothingOpens()
    {
        var onBacklog = Press(Backlog(), "G");

        var result = Press(onBacklog.State, "Space", "s", "e");

        Assert.Null(Active(result.State).Form);
        Assert.Contains("backlog", result.State.Message);
    }

    [Fact]
    public void GivenAClosedSprintView_WhenTheEditKeyIsPressed_ThenItIsRefusedAsReadOnly()
    {
        var closed = BacklogFixtures.Active with { State = SprintState.Closed };

        var result = Press(SprintView(closed), "Space", "s", "e");

        Assert.Null(Active(result.State).Form);
        Assert.Equal("Closed sprint: read only.", result.State.Message);
    }

    [Fact]
    public void GivenAFutureSprintView_WhenTheEditKeyIsPressed_ThenTheFormOpensForThatSprint()
    {
        var result = Press(SprintView(BacklogFixtures.Future), "Space", "s", "e");

        Assert.Equal("Edit sprint Sprint 2", Active(result.State).Form!.Target.Title);
    }

    [Fact]
    public void GivenTheFilledCreateForm_WhenSubmitted_ThenOneCreateWriteStartsWithNameGoalAndDates()
    {
        var state = Press(Backlog(), "Space", "s", "n").State;
        state = Type(state, "Sprint 9", "Enter", "Hit it", "Enter", "2026-11-04 09:00", "Enter", "2026-11-18 17:30");

        var submitted = Press(state, "Ctrl-s");

        var effect = Assert.IsType<ExecuteWriteEffect>(Assert.Single(submitted.Effects));
        var request = Assert.IsType<CreateSprintRequest>(effect.Request);
        Assert.Equal((6L, "Sprint 9", "Hit it"), (request.BoardId, request.Name, request.Goal));
        Assert.Equal(new DateTime(2026, 11, 4, 9, 0, 0), request.StartDate!.Value.LocalDateTime);
        Assert.Equal(new DateTime(2026, 11, 18, 17, 30, 0), request.EndDate!.Value.LocalDateTime);
        Assert.Equal("Creating sprint \"Sprint 9\"", submitted.State.Message);
        Assert.NotNull(Active(submitted.State).Form!.OperationId);
    }

    [Fact]
    public void GivenOnlyAName_WhenSubmitted_ThenGoalAndDatesAreLeftOutOfTheCreate()
    {
        var state = Type(Press(Backlog(), "Space", "s", "n").State, "Later");

        var submitted = Press(state, "Ctrl-s");

        var request = Assert.IsType<CreateSprintRequest>(
            Assert.IsType<ExecuteWriteEffect>(Assert.Single(submitted.Effects)).Request);
        Assert.Equal(("Later", null, null, null), (request.Name, request.Goal, request.StartDate, request.EndDate));
    }

    [Fact]
    public void GivenNoName_WhenSubmitted_ThenNothingIsSentAndTheNameIsRequired()
    {
        var state = Press(Backlog(), "Space", "s", "n").State;
        state = Type(state, "Enter", "just a goal");

        var submitted = Press(state, "Ctrl-s");

        Assert.Empty(submitted.Effects);
        Assert.Equal("Required", Active(submitted.State).Form!.Fields[0].Error);
    }

    [Fact]
    public void GivenADateThatDoesNotRead_WhenSubmitted_ThenNothingIsSentAndTheDateFieldSaysWhy()
    {
        var state = Press(Backlog(), "Space", "s", "n").State;
        state = Type(state, "S", "Enter", "Enter", "tomorrow");

        var submitted = Press(state, "Ctrl-s");

        Assert.Empty(submitted.Effects);
        Assert.Equal("Use yyyy-MM-dd HH:mm", Active(submitted.State).Form!.Fields[2].Error);
    }

    [Fact]
    public void GivenAnEditThatChangesOnlyTheGoal_WhenSubmitted_ThenOnlyTheGoalIsSent()
    {
        var state = Press(Backlog(), "Space", "s", "e").State;
        state = Type(state, "Enter", "!");

        var submitted = Press(state, "Ctrl-s");

        var request = Assert.IsType<EditSprintRequest>(
            Assert.IsType<ExecuteWriteEffect>(Assert.Single(submitted.Effects)).Request);
        Assert.Equal(new SprintEdit(null, "Ship the login!", null, null), request.Edit);
        Assert.Equal(BacklogFixtures.ActiveId, request.Before.Id);
    }

    [Fact]
    public void GivenAnEditThatChangesOnlyTheEnd_WhenSubmitted_ThenTheStartGoesAlongSoJiraCanCompare()
    {
        var state = Press(Backlog(), "Space", "s", "e").State;
        state = Type(state, "Enter", "Enter", "Enter", "Backspace", "5");

        var submitted = Press(state, "Ctrl-s");

        var request = Assert.IsType<EditSprintRequest>(
            Assert.IsType<ExecuteWriteEffect>(Assert.Single(submitted.Effects)).Request);
        Assert.Equal(BacklogFixtures.Active.StartDate, request.Edit.StartDate);
        Assert.NotEqual(BacklogFixtures.Active.EndDate, request.Edit.EndDate);
        Assert.Null(request.Edit.Name);
    }

    [Fact]
    public void GivenAnEditWithNothingChanged_WhenSubmitted_ThenNothingIsSent()
    {
        var state = Press(Backlog(), "Space", "s", "e").State;

        var submitted = Press(state, "Ctrl-s");

        Assert.Empty(submitted.Effects);
    }

    [Fact]
    public void GivenEscOnAFilledForm_WhenCancelled_ThenItAsksBeforeDiscardingAndSendsNothing()
    {
        var state = Type(Press(Backlog(), "Space", "s", "n").State, "x");

        var asked = Press(state, "Esc");
        var discarded = Press(asked.State, "y");

        Assert.True(Active(asked.State).Form!.DiscardPending);
        Assert.Empty(asked.Effects);
        Assert.Null(Active(discarded.State).Form);
        Assert.Empty(discarded.Effects);
        Assert.Empty(discarded.State.Session.Operations);
    }

    [Fact]
    public void GivenJiraRefusesTheEndDate_WhenTheWriteFails_ThenTheMessageIsAtTheEndFieldAndTheFormStaysOpen()
    {
        var sent = SubmittedCreate();
        var operation = sent.Session.Operations.Single();
        var error = new WriteError(
            WriteErrorKind.Validation,
            400,
            "The end must be after the start.",
            [new WriteFieldError("endDate", "The end must be after the start.")],
            []);

        var failed = _reducer.Reduce(sent, new WriteFailed(operation.Id, _now, error));

        var form = Active(failed.State).Form!;
        Assert.Equal("The end must be after the start.", form.Fields[3].Error);
        Assert.Equal(3, form.Focus);
        Assert.Null(form.OperationId);
        Assert.Equal(InputMode.Insert, failed.State.Mode);
        Assert.Empty(failed.State.Session.Operations);
    }

    [Fact]
    public void GivenAnUnaddressedRefusal_WhenTheWriteFails_ThenTheStatusNamesWhatWasNotDone()
    {
        var sent = SubmittedCreate();
        var operation = sent.Session.Operations.Single();
        var error = new WriteError(WriteErrorKind.Permission, 403, "No.", [], ["Not allowed."]);

        var failed = _reducer.Reduce(sent, new WriteFailed(operation.Id, _now, error));

        Assert.Equal("No permission to create sprint \"Sprint 9\": Not allowed.", failed.State.Message);
        Assert.NotNull(Active(failed.State).Form);
    }

    [Fact]
    public void GivenACreateThatSucceeded_WhenSettled_ThenTheFormClosesAndTheBoardTabsReload()
    {
        var sent = SubmittedCreate();
        var operation = sent.Session.Operations.Single();

        var done = _reducer.Reduce(sent, new WriteSucceeded(operation.Id, _now, []));

        Assert.Null(Active(done.State).Form);
        Assert.Equal(InputMode.Normal, done.State.Mode);
        Assert.Equal("Created sprint \"Sprint 9\"", done.State.Message);
        var load = Assert.IsType<LoadViewPageEffect>(Assert.Single(done.Effects));
        Assert.Equal(ViewSourceKind.ScrumBacklog, load.Definition.Source);
        Assert.Empty(done.State.Session.Operations);
    }

    [Fact]
    public void GivenALostReplyOfACreate_WhenUnknown_ThenItIsCheckedAndNeverRepeated()
    {
        var sent = SubmittedCreate();
        var operation = sent.Session.Operations.Single();
        var error = new WriteError(WriteErrorKind.UnknownOutcome, null, "lost", [], []);

        var unknown = _reducer.Reduce(sent, new WriteOutcomeUnknown(operation.Id, _now, error));
        var verified = _reducer.Reduce(
            unknown.State,
            new WriteVerified(operation.Id, _now, VerifyOutcome.Unreachable, []));

        Assert.IsType<VerifyWriteEffect>(unknown.Effects.OfType<VerifyWriteEffect>().Single());
        Assert.Contains("do not create it twice", verified.State.Message);
        Assert.Empty(verified.Effects.OfType<ExecuteWriteEffect>());
    }

    [Fact]
    public void GivenTheSprintViewMenu_WhenOpenedOnScrumViewsOnly_ThenCreateAndEditAreOffered()
    {
        var scrum = ContextMenus.ForView(Backlog())!;
        var jql = ContextMenus.ForView(Jql())!;

        Assert.Contains(scrum.Items, item => item.Action == ActionIds.SprintCreate);
        Assert.Contains(scrum.Items, item => item.Action == ActionIds.SprintEdit);
        Assert.DoesNotContain(jql.Items, item => item.Action == ActionIds.SprintCreate);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void GivenEitherScrumBoard_WhenCreating_ThenTheWriteNamesThatBoard(long board)
    {
        var definition = new ViewDefinition(ViewSourceKind.Sprint, $"{board}:active", ViewLayout.List, "Sprint");
        var state = Type(Press(Open(definition), "Space", "s", "n").State, "S");

        var submitted = Press(state, "Ctrl-s");

        var request = Assert.IsType<CreateSprintRequest>(
            Assert.IsType<ExecuteWriteEffect>(Assert.Single(submitted.Effects)).Request);
        Assert.Equal(board, request.BoardId);
    }

    private AppState SubmittedCreate()
    {
        var state = Type(Press(Backlog(), "Space", "s", "n").State, "Sprint 9");

        return Press(state, "Ctrl-s").State;
    }

    private AppState Type(AppState state, params string[] parts)
    {
        foreach (var part in parts)
        {
            var isKey = part is "Enter" or "Backspace" or "Esc";
            var keys = isKey ? [part] : part.Select(KeyOf).ToArray();

            state = Press(state, keys).State;
        }

        return state;
    }

    private static string KeyOf(char character)
    {
        return character == ' ' ? "Space" : character.ToString();
    }

    private ReduceResult Press(AppState state, params string[] keys)
    {
        var result = new ReduceResult(state, []);

        foreach (var key in keys)
        {
            result = _reducer.Reduce(result.State, new KeyPressed(key, _now));
        }

        return result;
    }

    private static TabState Active(AppState state)
    {
        return state.Session.Tabs.Single(tab => tab.Id == state.Session.Active);
    }

    private AppState Backlog()
    {
        var opened = Open(new ViewDefinition(ViewSourceKind.ScrumBacklog, "6", ViewLayout.List, "Scrum board"));
        var tab = Active(opened);
        var sprints = new[] { BacklogFixtures.Active, BacklogFixtures.Future, BacklogFixtures.EmptyFuture };
        var withSprints = _reducer.Reduce(opened, new SprintsLoaded(tab.Id, tab.RequestId, sprints)).State;
        var page = new IssuePage(BacklogFixtures.RankedRows, null, null);

        return _reducer.Reduce(withSprints, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
    }

    private AppState SprintView(Sprint sprint)
    {
        var definition = new ViewDefinition(ViewSourceKind.Sprint, $"6:{sprint.Id}", ViewLayout.List, sprint.Name);
        var opened = Open(definition);
        var tab = Active(opened);

        return _reducer.Reduce(opened, new SprintLoaded(tab.Id, tab.RequestId, sprint)).State;
    }

    private AppState Jql()
    {
        return Open(new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "A"));
    }

    private AppState Open(ViewDefinition definition)
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with
        {
            Profiles = profiles,
            Session = SiteSession.Empty(profiles.Active) with { Connection = new Connected("Me", "acc-me") },
        };
        var opened = _reducer.Reduce(start, new ViewOpenRequested(definition)).State;

        return _reducer.Reduce(opened, new ViewportChanged(20)).State;
    }
}
