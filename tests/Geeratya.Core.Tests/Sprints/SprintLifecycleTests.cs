using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Menus;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Tests.Backlog;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Tests.Sprints;

/// <summary>Start and complete a sprint through the keymap, forms, confirm and write pipeline (#186).</summary>
[Trait("Os", "Independent")]
public class SprintLifecycleTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenAnActiveSprintWithOpenIssuesAndAFutureSprint_WhenCompleteIsPressed_ThenAFormAsksWhereTheyGo()
    {
        var result = Press(Backlog(), "Space", "s", "c");

        var form = Active(result.State).Form!;
        Assert.Empty(result.Effects);
        Assert.Equal("Complete sprint Sprint 1", form.Target.Title);
        var field = Assert.Single(form.Fields);
        Assert.Equal(["backlog"], field.Value.Chosen);
        Assert.Equal(["backlog", "next"], field.Options.Select(option => option.Id));
        Assert.Contains("Sprint 2", field.Options[1].Label);
    }

    [Fact]
    public void GivenTheDefaultDestination_WhenTheFormIsSubmitted_ThenTheConfirmSaysBacklogCountsAndNoUndo()
    {
        var asked = Press(Press(Backlog(), "Space", "s", "c").State, "Ctrl-s");

        Assert.Equal(InputMode.Confirm, asked.State.Mode);
        Assert.Empty(asked.Effects);
        Assert.Empty(asked.State.Session.Operations);
        Assert.Null(Active(asked.State).Form);
        var text = asked.State.Confirm!.Message;
        Assert.Contains("\"Sprint 1\" (0 done, 2 open)", text);
        Assert.Contains("2 issues will be moved to the backlog", text);
        Assert.Contains("second step", text);
        Assert.Contains("Jira cannot undo this", text);
    }

    [Fact]
    public void GivenTheConfirmOfAComplete_WhenYIsPressed_ThenOneWriteStartsForTheOpenIssuesOfThatSprintOnly()
    {
        var asked = Press(Press(Backlog(), "Space", "s", "c").State, "Ctrl-s");

        var sent = Press(asked.State, "y");

        var request = Assert.IsType<CompleteSprintRequest>(
            Assert.IsType<ExecuteWriteEffect>(Assert.Single(sent.Effects)).Request);
        Assert.Equal(["A-3", "A-4"], request.OpenKeys);
        Assert.Null(request.MoveTo);
        Assert.Equal(6, request.BoardId);
        Assert.Equal(BacklogFixtures.ActiveId, request.Sprint.Id);
    }

    [Theory]
    [InlineData("n")]
    [InlineData("Esc")]
    public void GivenTheConfirmOfAComplete_WhenDeclined_ThenNothingIsSent(string answer)
    {
        var asked = Press(Press(Backlog(), "Space", "s", "c").State, "Ctrl-s");

        var declined = Press(asked.State, answer);

        Assert.Empty(declined.Effects);
        Assert.Empty(declined.State.Session.Operations);
        Assert.Equal(InputMode.Normal, declined.State.Mode);
    }

    [Fact]
    public void GivenTheNextSprintIsChosen_WhenTheFormBecomesARequest_ThenTheOpenIssuesGoThere()
    {
        var tab = Active(Backlog());
        var sprint = BacklogFixtures.Active;
        var target = new CompleteSprintFormTarget(6, sprint, SprintTally.Of(tab, sprint), BacklogFixtures.Future);
        var field = target.Fields()[0];
        var change = new FieldChange(field.Id, field.Kind, FormValue.OfChosen(["next"]), FormValue.Empty);

        var request = Assert.IsType<CompleteSprintRequest>(target.Request([change]));

        Assert.Equal(BacklogFixtures.Future, request.MoveTo);
        Assert.Contains("moved to sprint \"Sprint 2\"", request.AskText);
        Assert.Contains("moved 2 issues to sprint \"Sprint 2\"", request.DoneText);
    }

    [Fact]
    public void GivenNoFutureSprint_WhenCompleteIsPressed_ThenTheConfirmOpensAtOnceWithTheBacklog()
    {
        var result = Press(Backlog([BacklogFixtures.Active]), "Space", "s", "c");

        Assert.Equal(InputMode.Confirm, result.State.Mode);
        Assert.Null(Active(result.State).Form);
        Assert.Contains("moved to the backlog", result.State.Confirm!.Message);
    }

    [Fact]
    public void GivenAnActiveSprintWithNoOpenIssue_WhenCompleteIsPressed_ThenTheConfirmSaysThereIsNothingToMove()
    {
        var done = BacklogFixtures.Issue("A-9", BacklogFixtures.Active, 2) with
        {
            Status = new IssueStatus("3", "Done", "done"),
        };

        var result = Press(Backlog([BacklogFixtures.Active, BacklogFixtures.Future], [done]), "Space", "s", "c");

        Assert.Equal(InputMode.Confirm, result.State.Mode);
        Assert.Contains("(1 done, 0 open)", result.State.Confirm!.Message);
        Assert.Contains("No open issues to move", result.State.Confirm.Message);
    }

    [Fact]
    public void GivenNotAllIssuesAreLoaded_WhenCompleteIsPressed_ThenItIsRefusedBecauseTheOpenListCouldBeShort()
    {
        var state = Backlog(more: true);

        var result = Press(state, "Space", "s", "c");

        Assert.Null(Active(result.State).Form);
        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Contains("Not all issues", result.State.Message);
    }

    [Fact]
    public void GivenAFutureSprint_WhenCompleteIsPressed_ThenOnlyAnActiveOneIsAccepted()
    {
        var onFuture = Press(Backlog(), "j", "j").State;

        var result = Press(onFuture, "Space", "s", "c");

        Assert.Equal("Only an active sprint can be completed.", result.State.Message);
        Assert.Null(Active(result.State).Form);
    }

    [Fact]
    public void GivenAnActiveSprint_WhenStartIsPressed_ThenOnlyAFutureOneIsAccepted()
    {
        var result = Press(Backlog(), "Space", "s", "s");

        Assert.Equal("Only a future sprint can be started.", result.State.Message);
        Assert.Null(Active(result.State).Form);
    }

    [Fact]
    public void GivenAFutureSprintWithoutDates_WhenStartIsPressed_ThenAFormAsksForBothDatesPrefilled()
    {
        var onFuture = Press(Backlog(), "j", "j").State;

        var result = Press(onFuture, "Space", "s", "s");

        var form = Active(result.State).Form!;
        Assert.Empty(result.Effects);
        Assert.Equal("Start sprint Sprint 2", form.Target.Title);
        Assert.Equal(["startDate", "endDate"], form.Fields.Select(field => field.Id));
        Assert.All(form.Fields, field => Assert.True(field.Required));
        Assert.True(FormValueParser.TryDateTime(form.Fields[0].Value.Text, out var start));
        Assert.True(FormValueParser.TryDateTime(form.Fields[1].Value.Text, out var end));
        Assert.Equal(_now.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), form.Fields[0].Value.Text);
        Assert.Equal(TimeSpan.FromDays(14), end - start);
    }

    [Fact]
    public void GivenTheDatesForm_WhenSubmitted_ThenTheConfirmNamesIssuesPointsAndDatesAndYStartsTheWrite()
    {
        var onFuture = Press(Backlog(), "j", "j").State;
        var asked = Press(Press(onFuture, "Space", "s", "s").State, "Ctrl-s");

        Assert.Equal(InputMode.Confirm, asked.State.Mode);
        var text = asked.State.Confirm!.Message;
        Assert.StartsWith("Start \"Sprint 2\": 2 issues, 3 pts, ", text);
        Assert.EndsWith("Jira cannot undo this.", text);
        Assert.Equal("Start sprint", asked.State.Confirm.Label);

        var sent = Press(asked.State, "y");

        var request = Assert.IsType<StartSprintRequest>(
            Assert.IsType<ExecuteWriteEffect>(Assert.Single(sent.Effects)).Request);
        Assert.Equal(BacklogFixtures.FutureId, request.Sprint.Id);
        Assert.True(request.End > request.Start);
    }

    [Fact]
    public void GivenAFutureSprintWithBothDates_WhenStartIsPressed_ThenTheConfirmOpensWithoutAForm()
    {
        var dated = BacklogFixtures.Future with
        {
            StartDate = new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 12, 15, 9, 0, 0, TimeSpan.Zero),
        };
        var state = Backlog([BacklogFixtures.Active, dated, BacklogFixtures.EmptyFuture]);

        var result = Press(state, "j", "j");
        result = Press(result.State, "Space", "s", "s");

        Assert.Equal(InputMode.Confirm, result.State.Mode);
        Assert.Null(Active(result.State).Form);
        Assert.Contains("Start \"Sprint 2\"", result.State.Confirm!.Message);
    }

    [Fact]
    public void GivenASprintViewOfAnActiveSprint_WhenCompleteIsPressed_ThenItCountsTheViewRowsAndMovesToTheBacklog()
    {
        var state = SprintView(BacklogFixtures.Active);

        var result = Press(state, "Space", "s", "c");

        Assert.Equal(InputMode.Confirm, result.State.Mode);
        Assert.Contains("(0 done, 2 open)", result.State.Confirm!.Message);
    }

    [Fact]
    public void GivenAnotherSprintWriteOnTheBoard_WhenCompleteIsConfirmed_ThenItIsRefusedAsBusy()
    {
        var asked = Press(Press(Backlog(), "Space", "s", "c").State, "Ctrl-s").State;
        var other = new CreateSprintRequest(6, "Other", null, null, null);
        var running = _reducer.Reduce(asked with { Mode = InputMode.Normal }, new WriteRequested(
            Active(asked).Id,
            other,
            _now)).State;
        var again = running with { Mode = InputMode.Confirm, Confirm = asked.Confirm };

        var sent = Press(again, "y");

        Assert.Empty(sent.Effects);
        Assert.StartsWith("The board is busy", sent.State.Message);
        Assert.Single(sent.State.Session.Operations);
    }

    [Fact]
    public void GivenACompleteThatSucceeded_WhenSettled_ThenTheBoardReloadsAndTheStatusNamesTheMove()
    {
        var sent = Press(Press(Press(Backlog(), "Space", "s", "c").State, "Ctrl-s").State, "y").State;
        var operation = sent.Session.Operations.Single();

        var done = _reducer.Reduce(sent, new WriteSucceeded(operation.Id, _now, []));

        Assert.Equal("Completed sprint \"Sprint 1\", moved 2 issues to the backlog", done.State.Message);
        Assert.Contains(done.Effects, effect => effect is LoadViewPageEffect);
        Assert.Contains(done.Effects, effect => effect is InvalidateCacheEffect);
        Assert.Empty(done.State.Session.Operations);
    }

    [Fact]
    public void GivenOnlyTheMoveFailed_WhenSettled_ThenTheBoardStillReloadsAndTheWarningSaysWhatStayed()
    {
        var sent = Press(Press(Press(Backlog(), "Space", "s", "c").State, "Ctrl-s").State, "y").State;
        var operation = sent.Session.Operations.Single();
        const string warning = "Completed \"Sprint 1\", but its 2 issues were not moved to the backlog: No.";

        var done = _reducer.Reduce(sent, new WriteSucceeded(operation.Id, _now, [], Warning: warning));

        Assert.Equal(warning, done.State.Message);
        Assert.Equal(StatusSeverity.Warning, done.State.Status!.Severity);
        Assert.Contains(done.Effects, effect => effect is LoadViewPageEffect);
    }

    [Fact]
    public void GivenALostCompleteReplyThatTheCheckFoundApplied_WhenVerified_ThenTheStatusSaysTheIssuesWereNotMoved()
    {
        var sent = Press(Press(Press(Backlog(), "Space", "s", "c").State, "Ctrl-s").State, "y").State;
        var operation = sent.Session.Operations.Single();
        var error = new WriteError(WriteErrorKind.UnknownOutcome, null, "lost", [], []);
        var unknown = _reducer.Reduce(sent, new WriteOutcomeUnknown(operation.Id, _now, error));

        var verified = _reducer.Reduce(
            unknown.State,
            new WriteVerified(operation.Id, _now, VerifyOutcome.Applied, []));

        Assert.Contains("is completed", verified.State.Message);
        Assert.Contains("were not moved", verified.State.Message);
        Assert.Contains(verified.Effects, effect => effect is LoadViewPageEffect);
    }

    [Fact]
    public void GivenAnEndBeforeTheStart_WhenTheDatesFormIsSubmitted_ThenNoQuestionIsAskedAndTheEndFieldShowsTheRefusal()
    {
        var onFuture = Press(Backlog(), "j", "j").State;
        var opened = Press(onFuture, "Space", "s", "s").State;
        var end = Active(opened).Form!.Fields[1];
        var typed = Press(opened, ["Enter", .. Enumerable.Repeat("Backspace", end.Value.Text.Length)]);
        var keys = "2020-01-01 09:00".Select(character => character == ' ' ? "Space" : character.ToString());
        var early = Press(typed.State, [.. keys]);

        var submitted = Press(early.State, "Ctrl-s");

        var request = Assert.IsType<StartSprintRequest>(
            Assert.IsType<ExecuteWriteEffect>(Assert.Single(submitted.Effects)).Request);
        Assert.True(request.End <= request.Start);
        Assert.Null(request.AskText);
        Assert.Equal(InputMode.Insert, submitted.State.Mode);
    }

    [Fact]
    public void GivenJiraAnswersNotFoundToTheComplete_WhenFailed_ThenTheTextNamesTheSprintAndNoIssueIsMarked()
    {
        var sent = Press(Press(Press(Backlog(), "Space", "s", "c").State, "Ctrl-s").State, "y").State;
        var operation = sent.Session.Operations.Single();
        var error = new WriteError(WriteErrorKind.NotFound, 404, "gone", [], []);

        var failed = _reducer.Reduce(sent, new WriteFailed(operation.Id, _now, error));

        Assert.Equal("complete sprint \"Sprint 1\" not found or no access", failed.State.Message);
        Assert.DoesNotContain("A-3", failed.State.Message);
        Assert.DoesNotContain(failed.Effects, effect => effect is InvalidateCacheEffect);
    }

    [Fact]
    public void GivenAScrumView_WhenTheViewMenuOpens_ThenStartAndCompleteAreOffered()
    {
        var menu = ContextMenus.ForView(Backlog())!;

        Assert.Contains(menu.Items, item => item.Action == ActionIds.SprintStart);
        Assert.Contains(menu.Items, item => item.Action == ActionIds.SprintComplete);
    }

    [Theory]
    [InlineData("s")]
    [InlineData("c")]
    public void GivenAJqlTab_WhenAKeyIsPressed_ThenTheStatusSaysSprintsNeedAScrumBoard(string key)
    {
        var jql = Open(new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "A"));

        var result = Press(jql, "Space", "s", key);

        Assert.Equal(SprintForms.NotScrumText, result.State.Message);
        Assert.Empty(result.Effects);
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

    private AppState Backlog(Sprint[]? sprints = null, IssueSummary[]? rows = null, bool more = false)
    {
        var opened = Open(new ViewDefinition(ViewSourceKind.ScrumBacklog, "6", ViewLayout.List, "Scrum board"));
        var tab = Active(opened);
        var known = sprints ?? [BacklogFixtures.Active, BacklogFixtures.Future, BacklogFixtures.EmptyFuture];
        var withSprints = _reducer.Reduce(opened, new SprintsLoaded(tab.Id, tab.RequestId, known)).State;
        var next = more ? new PageCursor("more") : null;
        var page = new IssuePage(rows ?? BacklogFixtures.RankedRows, next, null);

        return _reducer.Reduce(withSprints, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
    }

    private AppState SprintView(Sprint sprint)
    {
        var definition = new ViewDefinition(ViewSourceKind.Sprint, $"6:{sprint.Id}", ViewLayout.List, sprint.Name);
        var opened = Open(definition);
        var tab = Active(opened);
        var shown = _reducer.Reduce(opened, new SprintLoaded(tab.Id, tab.RequestId, sprint)).State;
        IssueSummary[] rows = [BacklogFixtures.Issue("A-3", sprint, 5), BacklogFixtures.Issue("A-4", sprint, null)];

        return _reducer.Reduce(shown, new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage(rows, null, null), _now))
            .State;
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
