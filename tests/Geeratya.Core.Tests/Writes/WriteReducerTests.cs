using System.Globalization;
using Geeratya.Core.Cache;
using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Tests.Writes;

[Trait("Os", "Independent")]
public class WriteReducerTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly Profile _dev = new("dev", "https://dev.atlassian.net", "me@dev.test");
    private static readonly IssueAssignee _eve = new("acc-1", "Eve");
    private static readonly IssueAssignee _bob = new("acc-2", "Bob");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenTwoTabsShowingAnIssue_WhenAssigning_ThenEveryTabShowsItAtOnceAndOneOperationRuns()
    {
        var state = TwoTabs();
        var origin = state.Session.Tabs[0].Id;

        var result = Request(state, origin, "A-1", _eve);

        var operation = Assert.Single(result.State.Session.Operations);
        var effect = Assert.IsType<ExecuteWriteEffect>(Assert.Single(result.Effects));
        Assert.Equal(_eve, result.State.Session.Tabs[0].Rows[0].Assignee);
        Assert.Equal(_eve, result.State.Session.Tabs[1].Rows[0].Assignee);
        Assert.Null(result.State.Session.Tabs[0].Rows[1].Assignee);
        Assert.Equal(OperationKind.Assign, operation.Kind);
        Assert.Equal(OperationPhase.Sending, operation.Phase);
        Assert.Equal(["A-1"], operation.Keys);
        Assert.Equal(origin, operation.Origin);
        Assert.Equal(_start, operation.StartedAt);
        Assert.Equal(operation.Id, effect.OperationId);
        Assert.Equal(state.Sequence + 1, operation.Id);
        Assert.Equal(operation.Id, result.State.Sequence);
        Assert.Equal("Assigning A-1 to Eve", result.State.Message);
        Assert.Equal(new AssigneeRowChange("A-1", null, _eve), Assert.Single(operation.Undo));
    }

    [Fact]
    public void GivenRunningWriteOnAnIssue_WhenAnotherWriteTouchesIt_ThenItIsRefusedWithoutAnyChange()
    {
        var started = Request(TwoTabs(), TwoTabs().Session.Tabs[0].Id, "A-1", _eve);

        var refused = Request(started.State, started.State.Session.Tabs[0].Id, "A-1", _bob);

        Assert.Equal("A-1 is busy (assigning)", refused.State.Message);
        Assert.Empty(refused.Effects);
        Assert.Single(refused.State.Session.Operations);
        Assert.Equal(started.State.Sequence, refused.State.Sequence);
        Assert.Equal(_eve, refused.State.Session.Tabs[0].Rows[0].Assignee);
    }

    [Fact]
    public void GivenRunningWriteFromAnotherTab_WhenThisTabWritesTheSameIssueInOtherCase_ThenItIsRefused()
    {
        var state = TwoTabs();
        var started = Request(state, state.Session.Tabs[0].Id, "A-1", _eve);

        var refused = Request(started.State, state.Session.Tabs[1].Id, "a-1", _bob);

        Assert.Equal("a-1 is busy (assigning)", refused.State.Message);
        Assert.Empty(refused.Effects);
    }

    [Fact]
    public void GivenRunningWrite_WhenAnotherIssueIsWritten_ThenBothRunInParallel()
    {
        var state = TwoTabs();
        var first = Request(state, state.Session.Tabs[0].Id, "A-1", _eve);

        var second = Request(first.State, state.Session.Tabs[0].Id, "A-2", _bob);

        Assert.Equal(2, second.State.Session.Operations.Count);
        Assert.NotEqual(second.State.Session.Operations[0].Id, second.State.Session.Operations[1].Id);
        Assert.IsType<ExecuteWriteEffect>(Assert.Single(second.Effects));
    }

    [Fact]
    public void GivenNoActiveProfile_WhenWriting_ThenTheStatusLineSaysSoAndNothingStarts()
    {
        var result = Request(AppState.Initial, new TabId(1), "A-1", _eve);

        Assert.Equal("No profile is active.", result.State.Message);
        Assert.Empty(result.State.Session.Operations);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenAnAccountOfTheProfile_WhenAssigningToMe_ThenTheOperationAssignsThatAccount()
    {
        var state = TwoTabs();

        var result = _reducer.Reduce(state, new AssignToMeRequested(state.Session.Tabs[0].Id, "A-1", _start));

        var effect = Assert.IsType<ExecuteWriteEffect>(Assert.Single(result.Effects));
        Assert.Equal(new AssignRequest("A-1", new IssueAssignee("acc-me", "Me Myself")), effect.Request);
    }

    [Fact]
    public void GivenNoKnownAccount_WhenAssigningToMe_ThenNothingStarts()
    {
        var state = TwoTabs();
        var notChecked = state with { Session = state.Session with { Connection = new Unchecked() } };
        var nameOnly = state with { Session = state.Session with { Connection = new Connected("Me") } };

        var first = _reducer.Reduce(notChecked, new AssignToMeRequested(state.Session.Tabs[0].Id, "A-1", _start));
        var second = _reducer.Reduce(nameOnly, new AssignToMeRequested(state.Session.Tabs[0].Id, "A-1", _start));

        Assert.Empty(first.Effects);
        Assert.Empty(second.Effects);
        Assert.Contains("Not connected", first.State.Message);
        Assert.Empty(second.State.Session.Operations);
    }

    [Fact]
    public void GivenRunningWrite_WhenTheClockTicksPastTheRefreshTime_ThenNoTabRefreshes()
    {
        var started = Request(TwoTabs(), new TabId(1), "A-1", _eve);

        var tick = _reducer.Reduce(started.State, new Tick(_start + TimeSpan.FromMinutes(10)));

        Assert.Empty(tick.Effects);
        Assert.Same(started.State, tick.State);
    }

    [Fact]
    public void GivenAWriteThatEnded_WhenTheClockTicksPastTheRefreshTime_ThenTheActiveTabRefreshes()
    {
        var started = Request(TwoTabs(), new TabId(1), "A-1", _eve);
        var operation = started.State.Session.Operations[0];
        var done = _reducer.Reduce(started.State, new WriteSucceeded(operation.Id, _start, [])).State;

        var tick = _reducer.Reduce(done, new Tick(_start + TimeSpan.FromMinutes(10)));

        Assert.IsType<LoadViewPageEffect>(Assert.Single(tick.Effects));
    }

    [Fact]
    public void GivenRunningWrite_WhenTheRefreshKeyIsPressed_ThenItIsRememberedAndRunsAfterTheLastWrite()
    {
        var state = TwoTabs();
        var tab = state.Session.Tabs[0].Id;
        var first = Request(state, tab, "A-1", _eve);
        var second = Request(first.State, tab, "A-2", _bob);

        var asked = _reducer.Reduce(second.State, new RefreshRequested(tab));
        var firstId = first.State.Session.Operations[0].Id;
        var lastId = second.State.Session.Operations[1].Id;
        var afterFirst = _reducer.Reduce(asked.State, new WriteSucceeded(firstId, _start, []));
        var afterLast = _reducer.Reduce(afterFirst.State, new WriteSucceeded(lastId, _start, []));

        Assert.Empty(asked.Effects);
        Assert.Equal("Refresh after the write", asked.State.Message);
        Assert.True(asked.State.Session.Tabs[0].RefreshQueued);
        Assert.DoesNotContain(afterFirst.Effects, effect => effect is LoadViewPageEffect);
        Assert.True(afterFirst.State.Session.Tabs[0].RefreshQueued);
        var load = Assert.Single(afterLast.Effects.OfType<LoadViewPageEffect>());
        Assert.Equal(tab, load.Tab);
        Assert.False(load.Background);
        Assert.Equal(LoadReason.Refresh, load.Reason);
        Assert.False(afterLast.State.Session.Tabs[0].RefreshQueued);
    }

    [Fact]
    public void GivenRunningWrite_WhenTheRefreshKeyIsPressedAndTheWriteFails_ThenTheQueuedRefreshStillRuns()
    {
        var state = TwoTabs();
        var tab = state.Session.Tabs[0].Id;
        var started = Request(state, tab, "A-1", _eve);
        var asked = _reducer.Reduce(started.State, new RefreshRequested(tab));

        var failed = Fail(asked.State, Error(WriteErrorKind.Permission));

        Assert.IsType<LoadViewPageEffect>(Assert.Single(failed.Effects));
    }

    [Fact]
    public void GivenAcceptedWrite_WhenTheServerRowArrives_ThenEveryTabTakesItAndCachesAreInvalidated()
    {
        var state = TwoTabs();
        var started = Request(state, state.Session.Tabs[0].Id, "A-1", _eve);
        var serverRow = Issue(1) with { Assignee = _eve, Summary = "Renamed by a post function" };

        var done = _reducer.Reduce(
            started.State,
            new WriteSucceeded(started.State.Session.Operations[0].Id, _start, [serverRow]));

        Assert.Empty(done.State.Session.Operations);
        Assert.Equal("Renamed by a post function", done.State.Session.Tabs[0].Rows[0].Summary);
        Assert.Equal("Renamed by a post function", done.State.Session.Tabs[1].Rows[0].Summary);
        Assert.True(done.State.Session.Tabs[0].IsStale);
        Assert.True(done.State.Session.Tabs[1].IsStale);
        Assert.Equal("Assigned A-1 to Eve", done.State.Message);
        Assert.Equal(["A-1"], Assert.Single(done.Effects.OfType<InvalidateCacheEffect>()).Keys);
    }

    [Fact]
    public void GivenAcceptedWriteWithoutServerRows_WhenItSucceeds_ThenTheOptimisticRowStaysUntilTheNextReload()
    {
        var state = TwoTabs();
        var started = Request(state, state.Session.Tabs[0].Id, "A-1", _eve);

        var done = Settle(started.State);

        Assert.Equal(_eve, done.State.Session.Tabs[0].Rows[0].Assignee);
        Assert.Empty(done.State.Session.Operations);
    }

    [Fact]
    public void GivenAReplyForAnOperationThatIsGone_WhenItArrives_ThenNothingChanges()
    {
        var state = TwoTabs();

        var success = _reducer.Reduce(state, new WriteSucceeded(99, _start, [Issue(1)]));
        var failure = _reducer.Reduce(state, new WriteFailed(99, _start, Error(WriteErrorKind.Permission)));
        var unknown = _reducer.Reduce(state, new WriteOutcomeUnknown(99, _start, Error(WriteErrorKind.Timeout)));
        var verified = _reducer.Reduce(state, new WriteVerified(99, _start, VerifyOutcome.Applied, []));

        Assert.Same(state, success.State);
        Assert.Same(state, failure.State);
        Assert.Same(state, unknown.State);
        Assert.Same(state, verified.State);
    }

    [Fact]
    public void GivenAnOperationThatEnded_WhenItsReplyArrivesAgain_ThenTheSecondOneIsIgnored()
    {
        var started = Request(TwoTabs(), new TabId(1), "A-1", _eve);
        var id = started.State.Session.Operations[0].Id;
        var done = _reducer.Reduce(started.State, new WriteSucceeded(id, _start, [])).State;

        var again = _reducer.Reduce(done, new WriteFailed(id, _start, Error(WriteErrorKind.Permission)));

        Assert.Same(done, again.State);
        Assert.Equal(_eve, again.State.Session.Tabs[0].Rows[0].Assignee);
    }

    [Theory]
    [InlineData(WriteErrorKind.Permission, "No permission to assign A-1: Jira said no")]
    [InlineData(WriteErrorKind.Validation, "Could not assign A-1: Jira said no")]
    [InlineData(WriteErrorKind.NotFound, "A-1 not found or no access")]
    [InlineData(WriteErrorKind.Conflict, "A-1 changed meanwhile; reload and try again")]
    [InlineData(WriteErrorKind.NotSent, "Could not assign A-1: Offline: nothing was sent")]
    public void GivenADefiniteFailure_WhenItArrives_ThenEveryTabGoesBackAndTheReasonIsShown(
        WriteErrorKind kind,
        string expected)
    {
        var state = TwoTabs();
        var started = Request(state, state.Session.Tabs[0].Id, "A-1", _eve);

        var failed = Fail(started.State, Error(kind));

        Assert.Null(failed.State.Session.Tabs[0].Rows[0].Assignee);
        Assert.Null(failed.State.Session.Tabs[1].Rows[0].Assignee);
        Assert.Empty(failed.State.Session.Operations);
        Assert.Equal(expected, failed.State.Message);
    }

    [Fact]
    public void GivenAFieldRejectedByJira_WhenItFails_ThenTheFieldMessageIsShown()
    {
        var started = Request(TwoTabs(), new TabId(1), "A-1", _eve);
        var field = new WriteFieldError("assignee", "Nope");
        var error = new WriteError(WriteErrorKind.Validation, 400, "bad", [field], []);

        var failed = Fail(started.State, error);

        Assert.Equal("Could not assign A-1: Nope", failed.State.Message);
    }

    [Fact]
    public void GivenRateLimitedWrite_WhenItFails_ThenTheWaitIsShownAndNothingIsRetried()
    {
        var started = Request(TwoTabs(), new TabId(1), "A-1", _eve);
        var error = Error(WriteErrorKind.RateLimited) with { RetryAfter = TimeSpan.FromSeconds(7.2) };

        var failed = Fail(started.State, error);

        Assert.Equal("Could not assign A-1: try again in 8 s", failed.State.Message);
        Assert.Empty(failed.Effects);
    }

    [Fact]
    public void GivenUnauthorizedWrite_WhenItFails_ThenTheLinkIsRejectedLikeForLoads()
    {
        var started = Request(TwoTabs(), new TabId(1), "A-1", _eve);

        var failed = Fail(started.State, Error(WriteErrorKind.Unauthorized));

        Assert.IsType<Rejected>(failed.State.Session.Link);
        Assert.Null(failed.State.Session.Tabs[0].Rows[0].Assignee);
    }

    [Fact]
    public void GivenTheIssueIsGone_WhenTheWriteFails_ThenTabsAreStaleAndCachesDropped()
    {
        var started = Request(TwoTabs(), new TabId(1), "A-1", _eve);

        var failed = Fail(started.State, Error(WriteErrorKind.NotFound));

        Assert.True(failed.State.Session.Tabs[0].IsStale);
        Assert.IsType<InvalidateCacheEffect>(Assert.Single(failed.Effects));
    }

    [Fact]
    public void GivenARowThatAReloadChangedMeanwhile_WhenTheWriteFails_ThenTheNewerValueIsKept()
    {
        var state = TwoTabs();
        var started = Request(state, state.Session.Tabs[0].Id, "A-1", _eve);
        var tabs = started.State.Session.Tabs;
        var changed = Retab(tabs[0], Issue(1) with { Assignee = _bob });
        var reloaded = started.State with { Session = started.State.Session with { Tabs = [changed, tabs[1]] } };

        var failed = Fail(reloaded, Error(WriteErrorKind.Permission));

        Assert.Equal(_bob, failed.State.Session.Tabs[0].Rows[0].Assignee);
        Assert.Null(failed.State.Session.Tabs[1].Rows[0].Assignee);
    }

    [Fact]
    public void GivenALostReply_WhenItArrives_ThenTheRowStaysTheIssueIsCheckedAndNothingIsSentAgain()
    {
        var started = Request(TwoTabs(), new TabId(1), "A-1", _eve);
        var id = started.State.Session.Operations[0].Id;

        var unknown = Lose(started.State, id);

        Assert.Equal(OperationPhase.Verifying, Assert.Single(unknown.State.Session.Operations).Phase);
        Assert.Equal(_eve, unknown.State.Session.Tabs[0].Rows[0].Assignee);
        Assert.Equal("A-1: not sure the assignment worked, checking", unknown.State.Message);
        Assert.Equal(id, Assert.Single(unknown.Effects.OfType<VerifyWriteEffect>()).OperationId);
        Assert.Empty(unknown.Effects.OfType<ExecuteWriteEffect>());
        Assert.Single(unknown.Effects.OfType<InvalidateCacheEffect>());
    }

    [Fact]
    public void GivenAFailureThatIsReallyUnknown_WhenItIsReportedAsFailed_ThenItIsStillVerifiedNotRolledBack()
    {
        var started = Request(TwoTabs(), new TabId(1), "A-1", _eve);
        var id = started.State.Session.Operations[0].Id;

        var result = _reducer.Reduce(started.State, new WriteFailed(id, _start, Error(WriteErrorKind.UnknownOutcome)));

        Assert.Equal(_eve, result.State.Session.Tabs[0].Rows[0].Assignee);
        Assert.IsType<VerifyWriteEffect>(result.Effects[0]);
    }

    [Fact]
    public void GivenAnOperationBeingVerified_WhenAnotherUnknownArrives_ThenItIsNotVerifiedTwice()
    {
        var started = Request(TwoTabs(), new TabId(1), "A-1", _eve);
        var id = started.State.Session.Operations[0].Id;
        var first = _reducer.Reduce(started.State, new WriteOutcomeUnknown(id, _start, Error(WriteErrorKind.Timeout)));

        var second = _reducer.Reduce(first.State, new WriteOutcomeUnknown(id, _start, Error(WriteErrorKind.Timeout)));

        Assert.Empty(second.Effects);
        Assert.Same(first.State, second.State);
    }

    [Fact]
    public void GivenAnOperationBeingVerified_WhenAnotherWriteTouchesTheIssue_ThenItIsStillBusy()
    {
        var started = Request(TwoTabs(), new TabId(1), "A-1", _eve);
        var id = started.State.Session.Operations[0].Id;
        var unknown = Lose(started.State, id);

        var refused = Request(unknown.State, new TabId(1), "A-1", _bob);

        Assert.Equal("A-1 is busy (assigning)", refused.State.Message);
    }

    [Fact]
    public void GivenTheCheckFoundTheChange_WhenVerified_ThenItFinishesAsASuccess()
    {
        var started = Request(TwoTabs(), new TabId(1), "A-1", _eve);
        var id = started.State.Session.Operations[0].Id;
        var unknown = Lose(started.State, id);

        var row = Issue(1) with { Assignee = _eve };
        var done = _reducer.Reduce(unknown.State, new WriteVerified(id, _start, VerifyOutcome.Applied, [row]));

        Assert.Empty(done.State.Session.Operations);
        Assert.Equal("Assigned A-1 to Eve", done.State.Message);
        Assert.Equal(_eve, done.State.Session.Tabs[1].Rows[0].Assignee);
    }

    [Fact]
    public void GivenTheCheckFoundNoChange_WhenVerified_ThenTheRowsGoBackAndTheMessageSaysItWasNotApplied()
    {
        var started = Request(TwoTabs(), new TabId(1), "A-1", _eve);
        var id = started.State.Session.Operations[0].Id;
        var unknown = Lose(started.State, id);

        var done = _reducer.Reduce(unknown.State, new WriteVerified(id, _start, VerifyOutcome.NotApplied, []));

        Assert.Empty(done.State.Session.Operations);
        Assert.Null(done.State.Session.Tabs[0].Rows[0].Assignee);
        Assert.Equal("A-1: the assignment was not applied", done.State.Message);
    }

    [Fact]
    public void GivenTheCheckCouldNotRead_WhenVerified_ThenRowsGoBackAreStaleAndTheOutcomeIsSaidUnknown()
    {
        var started = Request(TwoTabs(), new TabId(1), "A-1", _eve);
        var id = started.State.Session.Operations[0].Id;
        var unknown = Lose(started.State, id);

        var done = _reducer.Reduce(unknown.State, new WriteVerified(id, _start, VerifyOutcome.Unreachable, []));

        Assert.Null(done.State.Session.Tabs[0].Rows[0].Assignee);
        Assert.True(done.State.Session.Tabs[0].IsStale);
        Assert.Contains("could not check the result", done.State.Message);
        Assert.Single(done.Effects.OfType<InvalidateCacheEffect>());
    }

    [Fact]
    public void GivenTheOriginTabIsClosed_WhenTheWriteFinishes_ThenOtherTabsAreUpdatedAndTheMessageShows()
    {
        var state = TwoTabs();
        var origin = state.Session.Tabs[0].Id;
        var started = Request(state, origin, "A-1", _eve);

        var closed = _reducer.Reduce(started.State, new TabClosed(origin));
        var done = _reducer.Reduce(
            closed.State,
            new WriteSucceeded(started.State.Session.Operations[0].Id, _start, [Issue(1) with { Assignee = _eve }]));

        Assert.Single(closed.State.Session.Operations);
        Assert.DoesNotContain(closed.Effects, effect => effect is ExecuteWriteEffect);
        Assert.Empty(done.State.Session.Operations);
        Assert.Equal(_eve, done.State.Session.Tabs[0].Rows[0].Assignee);
        Assert.Equal("Assigned A-1 to Eve", done.State.Message);
    }

    [Fact]
    public void GivenTheOriginTabIsClosed_WhenTheWriteFails_ThenTheOtherTabGoesBack()
    {
        var state = TwoTabs();
        var origin = state.Session.Tabs[0].Id;
        var started = Request(state, origin, "A-1", _eve);
        var closed = _reducer.Reduce(started.State, new TabClosed(origin));

        var failed = _reducer.Reduce(
            closed.State,
            new WriteFailed(started.State.Session.Operations[0].Id, _start, Error(WriteErrorKind.Permission)));

        Assert.Null(failed.State.Session.Tabs[0].Rows[0].Assignee);
    }

    [Fact]
    public void GivenARunningWrite_WhenTheProfileChanges_ThenTheOperationsAreDroppedAndOldRepliesAreIgnored()
    {
        var state = TwoTabs() with { Profiles = ProfileCatalog.Empty.With(_work).With(_dev).Activate("work") };
        var started = Request(state, state.Session.Tabs[0].Id, "A-1", _eve);
        var id = started.State.Session.Operations[0].Id;
        var catalog = started.State.Profiles.Activate("dev");
        var switched = _reducer.Reduce(started.State, new ProfilesChanged(catalog, "Switched."));

        var late = _reducer.Reduce(switched.State, new WriteSucceeded(id, _start, [Issue(1)]));

        Assert.Empty(switched.State.Session.Operations);
        Assert.Empty(switched.State.Session.Tabs);
        Assert.Same(switched.State, late.State);
    }

    [Fact]
    public void GivenARunningWrite_WhenSwitchingProfile_ThenItAsksFirstAndStatesTheLoss()
    {
        var state = TwoTabs() with { Profiles = ProfileCatalog.Empty.With(_work).With(_dev).Activate("work") };
        var started = Request(state, state.Session.Tabs[0].Id, "A-1", _eve);

        var asked = Palette(started.State, "profile switch dev");
        var no = _reducer.Reduce(asked.State, new KeyPressed("n"));
        var yes = _reducer.Reduce(asked.State, new KeyPressed("y"));

        Assert.Equal(InputMode.Confirm, asked.State.Mode);
        Assert.Empty(asked.Effects);
        Assert.Equal(
            "1 write is still running; its result will not be shown. Switch to dev anyway?",
            asked.State.Confirm!.Message);
        Assert.Empty(no.Effects);
        Assert.IsType<SaveProfilesEffect>(Assert.Single(yes.Effects));
    }

    [Fact]
    public void GivenARunningWrite_WhenRemovingTheActiveProfile_ThenTheQuestionNamesTheLostResult()
    {
        var state = TwoTabs() with { Profiles = ProfileCatalog.Empty.With(_work).With(_dev).Activate("work") };
        var started = Request(state, state.Session.Tabs[0].Id, "A-1", _eve);

        var active = Palette(started.State, "profile remove work");
        var other = Palette(started.State, "profile remove dev");

        Assert.Contains("1 write is still running", active.State.Confirm!.Message);
        Assert.DoesNotContain("write is still running", other.State.Confirm!.Message);
    }

    [Fact]
    public void GivenNoWrite_WhenSwitchingProfile_ThenItSwitchesWithoutAsking()
    {
        var state = TwoTabs() with { Profiles = ProfileCatalog.Empty.With(_work).With(_dev).Activate("work") };

        var result = Palette(state, "profile switch dev");

        Assert.IsType<SaveProfilesEffect>(Assert.Single(result.Effects));
    }

    [Theory]
    [InlineData(1, "1 write is still running; its result will not be shown. Quit anyway?")]
    [InlineData(2, "2 writes are still running; their results will not be shown. Quit anyway?")]
    public void GivenRunningWrites_WhenQuitting_ThenItAsksAndOnlyYesQuits(int count, string question)
    {
        var state = TwoTabs();
        var tab = state.Session.Tabs[0].Id;
        var running = Request(state, tab, "A-1", _eve).State;
        running = count == 2 ? Request(running, tab, "A-2", _bob).State : running;

        var asked = _reducer.Reduce(running, new KeyPressed("q"));
        var palette = Palette(running, "quit");
        var no = _reducer.Reduce(asked.State, new KeyPressed("n"));
        var yes = _reducer.Reduce(asked.State, new KeyPressed("y"));

        Assert.Equal(question, asked.State.Confirm!.Message);
        Assert.Empty(asked.Effects);
        Assert.Empty(palette.Effects);
        Assert.Equal(InputMode.Confirm, palette.State.Mode);
        Assert.Empty(no.Effects);
        Assert.IsType<QuitEffect>(Assert.Single(yes.Effects));
    }

    [Fact]
    public void GivenNoWrites_WhenQuitting_ThenItQuitsAtOnce()
    {
        var result = _reducer.Reduce(TwoTabs(), new KeyPressed("q"));

        Assert.IsType<QuitEffect>(Assert.Single(result.Effects));
    }

    [Fact]
    public void GivenALoadLandingDuringAWrite_WhenItsReplyArrives_ThenTheWrittenRowIsKeptAndOthersAreTaken()
    {
        var state = TwoTabs();
        var tab = state.Session.Tabs[1].Id;
        var loading = StartLoad(state, tab);
        var started = Request(loading.State, state.Session.Tabs[0].Id, "A-1", _eve);
        var reply = new IssuePage([Issue(1), Issue(2) with { Summary = "Fresh two" }], null, null);

        var landed = Land(started.State, tab, reply);

        var rows = landed.State.Session.Tabs[1].Rows;
        Assert.Equal(_eve, rows[0].Assignee);
        Assert.Equal("Fresh two", rows[1].Summary);
    }

    [Fact]
    public void GivenALoadInFlightWhenAWriteEnds_WhenItsReplyArrives_ThenTheTabsOwnRowIsKeptOnce()
    {
        var state = TwoTabs();
        var tab = state.Session.Tabs[1].Id;
        var loading = StartLoad(state, tab);
        var started = Request(loading.State, state.Session.Tabs[0].Id, "A-1", _eve);
        var id = started.State.Session.Operations[0].Id;
        var done = _reducer.Reduce(started.State, new WriteSucceeded(id, _start, [Issue(1) with { Assignee = _eve }]));
        var staleReply = new IssuePage([Issue(1), Issue(2)], null, null);

        var landed = Land(done.State, tab, staleReply);
        var secondLoad = StartLoad(landed.State, tab);
        var later = Land(secondLoad.State, tab, staleReply);

        Assert.Equal(["A-1"], done.State.Session.Tabs[1].ProtectedKeys);
        Assert.Equal(_eve, landed.State.Session.Tabs[1].Rows[0].Assignee);
        Assert.Empty(landed.State.Session.Tabs[1].ProtectedKeys);
        Assert.Null(later.State.Session.Tabs[1].Rows[0].Assignee);
    }

    [Fact]
    public void GivenALoadInFlightWhenAWriteFails_WhenItsReplyArrives_ThenTheReplyRowIsTaken()
    {
        var state = TwoTabs();
        var tab = state.Session.Tabs[1].Id;
        var loading = StartLoad(state, tab);
        var started = Request(loading.State, state.Session.Tabs[0].Id, "A-1", _eve);
        var failed = Fail(started.State, Error(WriteErrorKind.Permission));

        var landed = Land(failed.State, tab, new IssuePage([Issue(1), Issue(2)], null, null));

        Assert.Empty(failed.State.Session.Tabs[1].ProtectedKeys);
        Assert.Null(landed.State.Session.Tabs[1].Rows[0].Assignee);
    }

    [Fact]
    public void GivenNoWrite_WhenAPageLoads_ThenItsRowsAreTakenAsTheyAreWithoutCopying()
    {
        var state = TwoTabs();
        var tab = state.Session.Tabs[1];
        var page = new IssuePage([Issue(1), Issue(2)], null, null);

        var merged = WriteMerge.KeepHeldRows(state, tab, page.Items);

        Assert.Same(page.Items, merged);
    }

    [Fact]
    public void GivenANextPageInFlightDuringAWrite_WhenItAppends_ThenARowItBringsTakesTheWrittenValue()
    {
        var state = NextPageLoading(TwoTabs());
        var started = Request(state, state.Session.Tabs[0].Id, "A-1", _eve);

        var landed = LandAppend(started.State, new IssuePage([Issue(1), Issue(3)], null, null));

        var rows = landed.State.Session.Tabs[1].Rows;
        Assert.Equal(["A-2", "A-1", "A-3"], rows.Select(row => row.Key));
        Assert.Equal(_eve, rows[1].Assignee);
    }

    [Fact]
    public void GivenANextPageInFlightWhenAWriteEnds_WhenItAppends_ThenTheServerRowOfTheWriteIsKept()
    {
        var state = NextPageLoading(TwoTabs());
        var started = Request(state, state.Session.Tabs[0].Id, "A-1", _eve);
        var done = Settle(started.State, Issue(1) with { Assignee = _eve, Summary = "Server one" });

        var landed = LandAppend(done.State, new IssuePage([Issue(1), Issue(3)], null, null));

        var rows = landed.State.Session.Tabs[1].Rows;
        Assert.Equal(_eve, rows[1].Assignee);
        Assert.Equal("Server one", rows[1].Summary);
        Assert.Empty(landed.State.Session.Tabs[1].ProtectedRows);
    }

    [Fact]
    public void GivenANextPageInFlightWhenAWriteFails_WhenItAppends_ThenTheReplyRowIsTaken()
    {
        var state = NextPageLoading(TwoTabs());
        var started = Request(state, state.Session.Tabs[0].Id, "A-1", _eve);
        var failed = Fail(started.State, Error(WriteErrorKind.Permission));

        var landed = LandAppend(failed.State, new IssuePage([Issue(1), Issue(3)], null, null));

        Assert.Null(landed.State.Session.Tabs[1].Rows[1].Assignee);
    }

    [Fact]
    public void GivenADetailLoadingDuringAWrite_WhenItsReplyArrives_ThenTheWrittenFieldIsKept()
    {
        var state = OpenDetail(TwoTabs(), 1);
        var started = Request(state, state.Session.Tabs[0].Id, "A-1", _eve);

        var landed = LandDetail(started.State, 1, Detailed("A-1") with { Summary = "Fresh summary" });

        var issue = landed.State.Session.Tabs[1].Detail[0].Issue!;
        Assert.Equal(_eve, issue.Assignee);
        Assert.Equal("Fresh summary", issue.Summary);
    }

    [Fact]
    public void GivenADetailLoadingWhenAWriteEnds_WhenTheOlderReplyArrives_ThenTheWrittenFieldIsKeptOnce()
    {
        var state = OpenDetail(TwoTabs(), 0);
        var started = Request(state, state.Session.Tabs[1].Id, "A-1", _eve);
        var done = Settle(started.State, Issue(1) with { Assignee = _eve });

        var landed = LandDetail(done.State, 0, Detailed("A-1"));

        var detail = landed.State.Session.Tabs[0].Detail[0];
        Assert.Equal(_eve, detail.Issue!.Assignee);
        Assert.Null(detail.HeldChanges);
    }

    [Fact]
    public void GivenADetailLoadingWhenAWriteFails_WhenItsReplyArrives_ThenTheReplyIsTaken()
    {
        var state = OpenDetail(TwoTabs(), 1);
        var started = Request(state, state.Session.Tabs[0].Id, "A-1", _eve);
        var failed = Fail(started.State, Error(WriteErrorKind.Permission));

        var landed = LandDetail(failed.State, 1, Detailed("A-1"));

        Assert.Null(landed.State.Session.Tabs[1].Detail[0].Issue!.Assignee);
    }

    [Fact]
    public void GivenADetailReplyDuringAWrite_WhenTheWriteFails_ThenTheIssueIsReloadedAndNotCachedWithTheGuess()
    {
        var state = OpenDetail(TwoTabs(), 1);
        var started = Request(state, state.Session.Tabs[0].Id, "A-1", _eve);
        var landed = LandDetail(started.State, 1, Detailed("A-1"));

        var failed = Fail(landed.State, Error(WriteErrorKind.Permission));

        var cache = Assert.Single(landed.Effects.OfType<WriteIssueCacheEffect>());
        Assert.Null(cache.Issue.Detail.Assignee);
        Assert.Single(failed.Effects.OfType<LoadIssueDetailEffect>());
        Assert.True(failed.State.Session.Tabs[1].Detail[0].IssueStale);
    }

    [Fact]
    public void GivenAShownDetail_WhenTheWriteSucceeds_ThenItIsMarkedStaleAndReloadedAndTheOlderReplyIsDropped()
    {
        var loaded = LandDetail(OpenDetail(TwoTabs(), 1), 1, Detailed("A-1"));
        var oldRequest = loaded.State.Session.Tabs[1].Detail[0].IssueRequest;
        var started = Request(loaded.State, loaded.State.Session.Tabs[1].Id, "A-1", _eve);

        var done = Settle(started.State, Issue(1) with { Assignee = _eve });

        var detail = done.State.Session.Tabs[1].Detail[0];
        var reload = Assert.Single(done.Effects.OfType<LoadIssueDetailEffect>());
        Assert.True(detail.IssueStale);
        Assert.Equal("A-1", reload.Key);
        Assert.Equal(detail.IssueRequest, reload.RequestId);
        Assert.NotEqual(oldRequest, reload.RequestId);

        var tabId = done.State.Session.Tabs[1].Id;
        var late = _reducer.Reduce(done.State, new IssueDetailLoaded(tabId, oldRequest, Detailed("A-1"), _start));
        Assert.True(late.State.Session.Tabs[1].Detail[0].IssueStale);
        var fresh = LandDetail(done.State, 1, Detailed("A-1") with { Assignee = _eve });
        Assert.False(fresh.State.Session.Tabs[1].Detail[0].IssueStale);
    }

    [Fact]
    public void GivenAShownDetailInAHiddenTab_WhenTheWriteSucceeds_ThenItReloadsWhenTheTabIsActivated()
    {
        var hidden = LandDetail(OpenDetail(TwoTabs(), 0), 0, Detailed("A-1"));
        var started = Request(hidden.State, hidden.State.Session.Tabs[1].Id, "A-1", _eve);

        var done = Settle(started.State, Issue(1) with { Assignee = _eve });
        var activated = ViewReducer.Reduce(done.State, new TabActivated(done.State.Session.Tabs[0].Id));

        Assert.Empty(done.Effects.OfType<LoadIssueDetailEffect>());
        Assert.True(done.State.Session.Tabs[0].Detail[0].IssueStale);
        var reload = Assert.Single(activated.Effects.OfType<LoadIssueDetailEffect>());
        Assert.Equal("A-1", reload.Key);
    }

    private ReduceResult Request(AppState state, TabId tab, string key, IssueAssignee assignee)
    {
        return _reducer.Reduce(state, new WriteRequested(tab, new AssignRequest(key, assignee), _start));
    }

    private ReduceResult Palette(AppState state, string command)
    {
        var current = state with { Mode = InputMode.Command, PaletteText = command };

        return _reducer.Reduce(current, new KeyPressed("Enter"));
    }

    private static ReduceResult StartLoad(AppState state, TabId tab)
    {
        return ViewReducer.Reduce(state, new RefreshRequested(tab));
    }

    /// <summary>The reply of the load that is in flight for <paramref name="tab"/>.</summary>
    private ReduceResult Land(AppState state, TabId tab, IssuePage page)
    {
        var requestId = state.Session.Tabs.Single(candidate => candidate.Id == tab).RequestId;

        return _reducer.Reduce(state, new ViewPageLoaded(tab, requestId, page, _start));
    }

    /// <summary>Jira refuses the first running operation.</summary>
    private ReduceResult Fail(AppState state, WriteError error)
    {
        return _reducer.Reduce(state, new WriteFailed(state.Session.Operations[0].Id, _start, error));
    }

    /// <summary>Jira accepts the first running operation.</summary>
    private ReduceResult Settle(AppState state, params IssueSummary[] rows)
    {
        return _reducer.Reduce(state, new WriteSucceeded(state.Session.Operations[0].Id, _start, rows));
    }

    /// <summary>The reply of operation <paramref name="id"/> is lost.</summary>
    private ReduceResult Lose(AppState state, long id)
    {
        return _reducer.Reduce(state, new WriteOutcomeUnknown(id, _start, Error(WriteErrorKind.Timeout)));
    }

    /// <summary>The second tab shows page two of its source: it holds A-2 only and its next page is in flight.</summary>
    private static AppState NextPageLoading(AppState state)
    {
        var tab = state.Session.Tabs[1];
        var loading = tab with { Rows = [Issue(2)], Load = new Loading(LoadReason.NextPage), RequestId = 900 };

        var tabs = state.Session.Tabs.ToArray();

        tabs[1] = loading;

        return state with { Session = state.Session with { Tabs = tabs } };
    }

    private ReduceResult LandAppend(AppState state, IssuePage page)
    {
        var tab = state.Session.Tabs[1];

        return _reducer.Reduce(state, new ViewPageLoaded(tab.Id, tab.RequestId, page, _start));
    }

    /// <summary>Enter on the first row of the tab at <paramref name="index"/>, which is made active first.</summary>
    private AppState OpenDetail(AppState state, int index)
    {
        var active = ViewReducer.Reduce(state, new TabActivated(state.Session.Tabs[index].Id)).State;
        var opened = _reducer.Reduce(active, new KeyPressed("Enter")).State;

        return ViewReducer.Reduce(opened, new TabActivated(opened.Session.Tabs[1].Id)).State;
    }

    private ReduceResult LandDetail(AppState state, int index, IssueDetail issue)
    {
        var tab = state.Session.Tabs[index];

        return _reducer.Reduce(state, new IssueDetailLoaded(tab.Id, tab.Detail[0].IssueRequest, issue, _start));
    }

    private static IssueDetail Detailed(string key)
    {
        return new IssueDetail(
            "1",
            key,
            "Issue 1",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            null,
            [],
            null,
            null,
            "A",
            null,
            null,
            [],
            [],
            []);
    }

    private static TabState Retab(TabState tab, IssueSummary first)
    {
        return tab with { Rows = [first, .. tab.Rows.Skip(1)] };
    }

    private static WriteError Error(WriteErrorKind kind)
    {
        return new WriteError(kind, null, "Jira said no", [], []);
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
