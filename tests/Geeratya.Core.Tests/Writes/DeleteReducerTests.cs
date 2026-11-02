using System.Globalization;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Tests.Writes;

[Trait("Os", "Independent")]
public class DeleteReducerTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenSelectedRow_WhenPressingTheDeleteKeys_ThenSubtasksAreCountedBeforeAnythingIsAsked()
    {
        var state = TwoTabs();

        var result = Press(state, "Space", "i", "d");

        var effect = Assert.IsType<CountSubtasksEffect>(Assert.Single(result.Effects));
        Assert.Equal("A-1", effect.Key);
        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Equal("Checking A-1 for sub-tasks...", result.State.Message);
        Assert.Empty(result.State.Session.Operations);
        Assert.Equal(effect.RequestId, result.State.Session.PendingDelete!.RequestId);
    }

    [Fact]
    public void GivenSecondRowSelected_WhenDeleting_ThenThatRowIsTheTarget()
    {
        var state = TwoTabs();
        var moved = _reducer.Reduce(state, new KeyPressed("j")).State;

        var result = Press(moved, "Space", "i", "d");

        Assert.Equal("A-2", Assert.IsType<CountSubtasksEffect>(Assert.Single(result.Effects)).Key);
    }

    [Fact]
    public void GivenAnIssueOpenInTheDetail_WhenDeleting_ThenTheShownIssueIsTheTargetNotTheRowUnderIt()
    {
        var state = TwoTabs();
        var opened = _reducer.Reduce(state, new KeyPressed("j")).State;
        opened = _reducer.Reduce(opened, new KeyPressed("Enter")).State;

        var result = Press(opened, "Space", "i", "d");

        Assert.Equal("A-2", Assert.IsType<CountSubtasksEffect>(Assert.Single(result.Effects)).Key);
        Assert.Equal("Issue 2", result.State.Session.PendingDelete!.Summary);
    }

    [Fact]
    public void GivenNoRowsOrNoProfile_WhenDeleting_ThenTheStatusLineSaysSoAndNothingIsSent()
    {
        var empty = Press(AppState.Initial, "Space", "i", "d");
        var state = TwoTabs();
        var noRows = state with { Session = state.Session with { Tabs = [state.Session.Tabs[0] with { Rows = [] }] } };
        var none = Press(noRows with { Session = noRows.Session with { Active = noRows.Session.Tabs[0].Id } }, "Space", "i", "d");

        Assert.Empty(empty.Effects);
        Assert.Contains("Nothing to delete", empty.State.Message);
        Assert.Empty(none.Effects);
        Assert.Equal("No issue selected.", none.State.Message);
    }

    [Fact]
    public void GivenARunningWriteOnTheIssue_WhenDeleting_ThenItIsRefusedWithoutAQuestion()
    {
        var state = TwoTabs();
        var started = _reducer.Reduce(state, new WriteRequested(state.Session.Tabs[1].Id, Assign("A-1"), _start));

        var result = Press(started.State, "Space", "i", "d");

        Assert.Empty(result.Effects);
        Assert.Contains("A-1 is busy", result.State.Message);
        Assert.Null(result.State.Session.PendingDelete);
    }

    [Fact]
    public void GivenNoSubtasks_WhenTheCountArrives_ThenConfirmNamesKeyTitleAndNoUndoAndSendsNothing()
    {
        var asked = Press(TwoTabs(), "Space", "i", "d");

        var counted = Count(asked, 0, false);

        var confirm = counted.State.Confirm!;
        Assert.Equal(InputMode.Confirm, counted.State.Mode);
        Assert.Equal("Delete A-1? Jira cannot undo this. \"Issue 1\"", confirm.Message);
        Assert.Empty(counted.Effects);
        Assert.Empty(counted.State.Session.Operations);
        Assert.Null(counted.State.Session.PendingDelete);
        var request = Assert.IsType<DeleteIssueRequest>(Assert.IsType<WriteRequested>(confirm.OnConfirm).Request);
        Assert.False(request.DeleteSubtasks);
    }

    [Theory]
    [InlineData(1, false, "Delete A-1 and its 1 sub-task? Jira cannot undo this. \"Issue 1\"")]
    [InlineData(3, false, "Delete A-1 and its 3 sub-tasks? Jira cannot undo this. \"Issue 1\"")]
    [InlineData(50, true, "Delete A-1 and its more than 50 sub-tasks? Jira cannot undo this. \"Issue 1\"")]
    public void GivenSubtasks_WhenTheCountArrives_ThenConfirmCountsThemAndTheDeleteTakesThemAlong(
        int count,
        bool more,
        string text)
    {
        var asked = Press(TwoTabs(), "Space", "i", "d");

        var counted = Count(asked, count, more);

        Assert.Equal(text, counted.State.Confirm!.Message);
        var write = Assert.IsType<WriteRequested>(counted.State.Confirm.OnConfirm);
        Assert.True(Assert.IsType<DeleteIssueRequest>(write.Request).DeleteSubtasks);
    }

    [Fact]
    public void GivenALongTitleWithControlCharacters_WhenAsking_ThenTheTitleIsCleanedAndCutAtTheEnd()
    {
        var request = new DeleteIssueRequest("A-1", $"First\nline {new string('x', 80)}", 0);

        Assert.StartsWith("Delete A-1? Jira cannot undo this. \"First line xxx", request.ConfirmText);
        Assert.EndsWith("…\"", request.ConfirmText);
        Assert.DoesNotContain('\n', request.ConfirmText);
    }

    [Fact]
    public void GivenOpenConfirm_WhenAnswering_ThenOnlyYSendsAndEnterNEscAndOtherKeysSendNothing()
    {
        var confirm = Count(Press(TwoTabs(), "Space", "i", "d"), 2, false).State;

        foreach (var key in new[] { "n", "Esc", "Enter", "x", "Space" })
        {
            var answered = _reducer.Reduce(confirm, new KeyPressed(key));

            Assert.Empty(answered.Effects);
            Assert.Empty(answered.State.Session.Operations);
        }

        var declined = _reducer.Reduce(confirm, new KeyPressed("n"));
        var yes = _reducer.Reduce(confirm, new KeyPressed("y"));

        Assert.Equal(InputMode.Normal, declined.State.Mode);
        Assert.Equal("Cancelled.", declined.State.Message);
        var execute = Assert.IsType<ExecuteWriteEffect>(Assert.Single(yes.Effects));
        Assert.Equal(new DeleteIssueRequest("A-1", "Issue 1", 2), execute.Request);
        Assert.Equal(InputMode.Normal, yes.State.Mode);
        Assert.Equal("Deleting A-1", yes.State.Message);
        Assert.Equal(["A-1", "A-2"], yes.State.Session.Tabs[0].Rows.Select(row => row.Key));
    }

    [Fact]
    public void GivenAWriteStartedWhileTheConfirmWasOpen_WhenConfirming_ThenTheBusyRefusalWinsAtThatMoment()
    {
        var state = TwoTabs();
        var confirm = Count(Press(state, "Space", "i", "d"), 0, false).State;
        var busy = _reducer.Reduce(confirm, new WriteRequested(state.Session.Tabs[1].Id, Assign("A-1"), _start)).State;

        var yes = _reducer.Reduce(busy, new KeyPressed("y"));

        Assert.Empty(yes.Effects);
        Assert.Equal("A-1 is busy (assigning)", yes.State.Message);
    }

    [Fact]
    public void GivenAReplyOfAnOlderQuestionOrAnotherMode_WhenItArrives_ThenNoConfirmOpens()
    {
        var asked = Press(TwoTabs(), "Space", "i", "d");
        var id = asked.State.Session.PendingDelete!.RequestId;

        var stale = _reducer.Reduce(asked.State, new SubtasksCounted(id + 7, 0, false, _start));
        var helping = asked.State with { Mode = InputMode.Help };
        var moved = _reducer.Reduce(helping, new SubtasksCounted(id, 0, false, _start));

        Assert.Equal(InputMode.Normal, stale.State.Mode);
        Assert.NotNull(stale.State.Session.PendingDelete);
        Assert.Equal(InputMode.Help, moved.State.Mode);
        Assert.Null(moved.State.Confirm);
        Assert.Null(moved.State.Session.PendingDelete);
    }

    [Fact]
    public void GivenTheCountFailed_WhenItArrives_ThenNothingIsAskedAndTheReasonIsShown()
    {
        var asked = Press(TwoTabs(), "Space", "i", "d");
        var id = asked.State.Session.PendingDelete!.RequestId;

        var failed = _reducer.Reduce(asked.State, new SubtasksCountFailed(id, "offline"));

        Assert.Equal(InputMode.Normal, failed.State.Mode);
        Assert.Equal("Could not check A-1 for sub-tasks (offline); nothing was deleted", failed.State.Message);
        Assert.Null(failed.State.Session.PendingDelete);
        Assert.Empty(failed.Effects);
    }

    [Fact]
    public void GivenARunningDelete_WhenItSucceeds_ThenTheRowAndDetailGoFromEveryTabAndTheOriginReloads()
    {
        var state = OpenDetail(TwoTabs(), 0);
        var started = StartDelete(state, 2);
        var operation = started.State.Session.Operations[0];

        var done = _reducer.Reduce(started.State, new WriteSucceeded(operation.Id, _start, []));

        Assert.All(done.State.Session.Tabs, tab => Assert.Equal(["A-2"], tab.Rows.Select(row => row.Key)));
        Assert.All(done.State.Session.Tabs, tab => Assert.Empty(tab.Detail));
        Assert.Equal("A-2", done.State.Session.Tabs[0].SelectedKey);
        Assert.Empty(done.State.Session.Operations);
        Assert.Equal("Deleted A-1 and its 2 sub-tasks", done.State.Message);
        Assert.Equal(["A-1"], Assert.Single(done.Effects.OfType<InvalidateCacheEffect>()).Keys);
        var reload = Assert.Single(done.Effects.OfType<LoadViewPageEffect>());
        Assert.Equal(operation.Origin, reload.Tab);
        Assert.True(done.State.Session.Tabs[1].IsStale);
    }

    [Fact]
    public void GivenARunningDelete_WhenJiraRefuses_ThenTheRowStaysAndTheReasonIsShown()
    {
        var started = StartDelete(TwoTabs(), 0);
        var operation = started.State.Session.Operations[0];
        var error = new WriteError(WriteErrorKind.Permission, 403, "Jira said no", [], ["No permission."]);

        var failed = _reducer.Reduce(started.State, new WriteFailed(operation.Id, _start, error));

        Assert.All(failed.State.Session.Tabs, tab => Assert.Equal(["A-1", "A-2"], tab.Rows.Select(row => row.Key)));
        Assert.Equal("No permission to delete A-1: No permission.", failed.State.Message);
        Assert.Empty(failed.State.Session.Operations);
        Assert.Empty(failed.Effects.OfType<LoadViewPageEffect>());
    }

    [Fact]
    public void GivenALostReply_WhenTheIssueIsGone_ThenTheDeleteFinishesAsDone()
    {
        var started = StartDelete(TwoTabs(), 0);
        var operation = started.State.Session.Operations[0];
        var error = new WriteError(WriteErrorKind.UnknownOutcome, null, "timeout", [], []);

        var unknown = _reducer.Reduce(started.State, new WriteOutcomeUnknown(operation.Id, _start, error));
        var verified = _reducer.Reduce(
            unknown.State,
            new WriteVerified(operation.Id, _start, VerifyOutcome.Applied, []));

        Assert.IsType<VerifyWriteEffect>(unknown.Effects.OfType<Effect>().First());
        Assert.Equal(["A-2"], verified.State.Session.Tabs[0].Rows.Select(row => row.Key));
        Assert.Equal("Deleted A-1", verified.State.Message);
    }

    [Fact]
    public void GivenALostReply_WhenTheIssueIsStillThere_ThenTheRowStaysAndTheDeleteWasNotApplied()
    {
        var started = StartDelete(TwoTabs(), 0);
        var operation = started.State.Session.Operations[0];
        var error = new WriteError(WriteErrorKind.UnknownOutcome, null, "timeout", [], []);

        var unknown = _reducer.Reduce(started.State, new WriteOutcomeUnknown(operation.Id, _start, error));
        var verified = _reducer.Reduce(
            unknown.State,
            new WriteVerified(operation.Id, _start, VerifyOutcome.NotApplied, []));

        Assert.Equal(["A-1", "A-2"], verified.State.Session.Tabs[0].Rows.Select(row => row.Key));
        Assert.Equal("A-1: the delete was not applied", verified.State.Message);
    }

    [Fact]
    public void GivenDeleteKey_WhenReadingTheDefaultKeymap_ThenItIsBoundToTheLeaderChord()
    {
        var keymap = DefaultKeymap.Load();

        Assert.Equal("Space i d", keymap.FindKeys(InputMode.Normal, ActionIds.IssueDelete));
        Assert.NotNull(ActionCatalog.FindTitle(ActionIds.IssueDelete));
    }

    private ReduceResult StartDelete(AppState state, int subtasks)
    {
        var counted = Count(Press(state, "Space", "i", "d"), subtasks, false);

        return _reducer.Reduce(counted.State, new KeyPressed("y"));
    }

    private ReduceResult Count(ReduceResult asked, int count, bool more)
    {
        var id = asked.State.Session.PendingDelete!.RequestId;

        return _reducer.Reduce(asked.State, new SubtasksCounted(id, count, more, _start));
    }

    private ReduceResult Press(AppState state, params string[] keys)
    {
        var result = new ReduceResult(state, []);

        foreach (var key in keys)
        {
            result = _reducer.Reduce(result.State, new KeyPressed(key));
        }

        return result;
    }

    private static AssignRequest Assign(string key)
    {
        return new AssignRequest(key, new IssueAssignee("acc-1", "Eve"));
    }

    private AppState OpenDetail(AppState state, int tabIndex)
    {
        var id = state.Session.Tabs[tabIndex].Id;
        var active = state with { Session = state.Session with { Active = id } };
        var opened = _reducer.Reduce(active, new KeyPressed("Enter")).State;

        return opened with { Session = opened.Session with { Active = state.Session.Active } };
    }

    /// <summary>Two loaded tabs (the second is active), both holding A-1 and A-2, on a connected profile.</summary>
    private AppState TwoTabs()
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var state = AppState.Initial with
        {
            Profiles = profiles,
            Session = SiteSession.Empty(profiles.Active) with { Connection = new Connected("Me Myself", "acc-me") },
        };

        state = OpenLoaded(state, "project = A");

        return OpenLoaded(state, "project = B");
    }

    private AppState OpenLoaded(AppState state, string jql)
    {
        var definition = new ViewDefinition(ViewSourceKind.Jql, jql, ViewLayout.List, jql);
        var opened = _reducer.Reduce(state, new ViewOpenRequested(definition)).State;
        var tab = opened.Session.Tabs[^1];
        var page = new IssuePage([Issue(1), Issue(2)], null, null);

        return _reducer.Reduce(opened, new ViewPageLoaded(tab.Id, tab.RequestId, page, _start)).State;
    }

    private static IssueSummary Issue(int number)
    {
        return new IssueSummary(
            number.ToString(CultureInfo.InvariantCulture),
            $"A-{number}",
            $"Issue {number}",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
    }
}
