using System.Globalization;
using Geeratya.Core.Cache;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Settings;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Users;

namespace Geeratya.Core.Tests.Views;

[Trait("Os", "Independent")]
public class ViewRefreshTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private static readonly BoardConfiguration _configuration = new(
        4,
        "GCM board",
        BoardType.Kanban,
        [new BoardColumn("Backlog", ["1"]), new BoardColumn("Done", ["2"])],
        "10001",
        null,
        "customfield_10019",
        null);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenLoadedTab_WhenTickBeforeInterval_ThenNothingHappensAndStateIsTheSameInstance()
    {
        var state = Loaded(3);

        var result = _reducer.Reduce(state, new Tick(_start + TimeSpan.FromSeconds(59)));

        Assert.Same(state, result.State);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenLoadedTab_WhenTickAtInterval_ThenFirstPageIsReloadedInTheBackground()
    {
        var state = Loaded(3);
        var tab = state.Session.Tabs[0];

        var result = _reducer.Reduce(state, new Tick(_start + TimeSpan.FromSeconds(60)));

        var effect = Assert.IsType<LoadViewPageEffect>(Assert.Single(result.Effects));
        var loading = result.State.Session.Tabs[0];
        Assert.True(effect.Background);
        Assert.Null(effect.Cursor);
        Assert.Equal(LoadReason.Refresh, effect.Reason);
        Assert.Equal(tab.Id, effect.Tab);
        Assert.Equal(effect.RequestId, loading.RequestId);
        Assert.Equal(new Loading(LoadReason.Refresh, true), loading.Load);
        Assert.Equal(3, loading.Rows.Count);
    }

    [Fact]
    public void GivenRefreshInFlight_WhenTickAgain_ThenNoSecondLoadStarts()
    {
        var state = Loaded(3);
        var first = _reducer.Reduce(state, new Tick(_start + TimeSpan.FromSeconds(61)));

        var second = _reducer.Reduce(first.State, new Tick(_start + TimeSpan.FromSeconds(120)));

        Assert.Empty(second.Effects);
        Assert.Same(first.State, second.State);
    }

    [Fact]
    public void GivenTwoTabs_WhenTickAfterInterval_ThenOnlyTheActiveTabRefreshes()
    {
        var state = Loaded(3);
        var first = state.Session.Tabs[0];
        var second = Open(state, "project = B");
        var secondTab = second.Session.Tabs[1];
        var loaded = Reply(second, secondTab, Page(2, null), _start);

        var result = _reducer.Reduce(loaded, new Tick(_start + TimeSpan.FromMinutes(10)));

        var effect = Assert.IsType<LoadViewPageEffect>(Assert.Single(result.Effects));
        Assert.Equal(secondTab.Id, effect.Tab);
        Assert.NotEqual(first.Id, effect.Tab);
        Assert.Equal(new IdleState(), result.State.Session.Tabs[0].Load);
    }

    [Fact]
    public void GivenHiddenTabOlderThanSwitchAge_WhenActivated_ThenNextTickRefreshesIt()
    {
        var both = Open(Loaded(3), "project = B");
        var shown = Reply(both, both.Session.Tabs[1], Page(1, null), _start);
        var firstId = shown.Session.Tabs[0].Id;

        var activated = _reducer.Reduce(shown, new TabActivated(firstId));
        var early = _reducer.Reduce(activated.State, new Tick(_start + TimeSpan.FromSeconds(29)));
        var due = _reducer.Reduce(activated.State, new Tick(_start + TimeSpan.FromSeconds(30)));

        Assert.Empty(early.Effects);
        var effect = Assert.IsType<LoadViewPageEffect>(Assert.Single(due.Effects));
        Assert.Equal(firstId, effect.Tab);
        Assert.True(effect.Background);
    }

    [Fact]
    public void GivenRecentlyLoadedTab_WhenActivated_ThenItIsNotDueBeforeThirtySeconds()
    {
        var both = Open(Loaded(3), "project = B");
        var shown = Reply(both, both.Session.Tabs[1], Page(1, null), _start + TimeSpan.FromSeconds(50));
        var firstId = shown.Session.Tabs[0].Id;
        var activated = _reducer.Reduce(shown, new TabActivated(firstId)).State;

        var result = _reducer.Reduce(activated, new Tick(_start + TimeSpan.FromSeconds(55)));

        Assert.Equal(_start + TimeSpan.FromSeconds(30), activated.Session.Tabs[0].NextRefreshAt);
        Assert.Equal(firstId, Assert.IsType<LoadViewPageEffect>(Assert.Single(result.Effects)).Tab);
    }

    [Fact]
    public void GivenRefreshReply_WhenRowsMove_ThenSelectionStaysOnItsIssueKey()
    {
        var state = Loaded(5);
        var id = state.Session.Tabs[0].Id;
        var moved = _reducer.Reduce(state, new SelectionMoved(id, 3)).State;
        var tick = _reducer.Reduce(moved, new Tick(_start + TimeSpan.FromSeconds(60)));
        var tab = tick.State.Session.Tabs[0];
        var reordered = new IssuePage([Issue(9), Issue(8), Issue(4), Issue(1)], null, null);

        var result = _reducer.Reduce(
            tick.State,
            new ViewPageLoaded(id, tab.RequestId, reordered, _start + TimeSpan.FromSeconds(61)));

        var updated = result.State.Session.Tabs[0];
        Assert.Equal("A-4", updated.SelectedKey);
        Assert.Equal(2, updated.Selected);
        Assert.Equal(["A-9", "A-8", "A-4", "A-1"], updated.Rows.Select(issue => issue.Key));
        Assert.Equal(_start + TimeSpan.FromSeconds(121), updated.NextRefreshAt);
        Assert.IsType<WriteCacheEffect>(Assert.Single(result.Effects));
    }

    [Fact]
    public void GivenRefreshReply_WhenSelectedIssueIsGone_ThenNearestIndexIsKept()
    {
        var state = Loaded(5);
        var id = state.Session.Tabs[0].Id;
        var moved = _reducer.Reduce(state, new SelectionMoved(id, 4)).State;
        var tick = _reducer.Reduce(moved, new Tick(_start + TimeSpan.FromSeconds(60)));
        var tab = tick.State.Session.Tabs[0];
        var page = Page(3, null);

        var result = _reducer.Reduce(tick.State, new ViewPageLoaded(id, tab.RequestId, page, _start));

        var updated = result.State.Session.Tabs[0];
        Assert.Equal(2, updated.Selected);
        Assert.Equal("A-3", updated.SelectedKey);
    }

    [Fact]
    public void GivenTabClosedDuringRefresh_WhenReplyArrives_ThenItIsIgnored()
    {
        var state = Loaded(3);
        var id = state.Session.Tabs[0].Id;
        var tick = _reducer.Reduce(state, new Tick(_start + TimeSpan.FromSeconds(60)));
        var requestId = tick.State.Session.Tabs[0].RequestId;
        var closed = _reducer.Reduce(tick.State, new TabClosed(id)).State;

        var result = _reducer.Reduce(closed, new ViewPageLoaded(id, requestId, Page(1, null), _start));

        Assert.Same(closed, result.State);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenSupersededRefresh_WhenOldReplyArrives_ThenItIsIgnored()
    {
        var state = Loaded(3);
        var id = state.Session.Tabs[0].Id;
        var tick = _reducer.Reduce(state, new Tick(_start + TimeSpan.FromSeconds(60)));
        var oldId = tick.State.Session.Tabs[0].RequestId;
        var failed = Fail(tick.State, JiraErrorKind.ServerError, _start + TimeSpan.FromSeconds(61));
        var manual = _reducer.Reduce(failed, new RefreshRequested(id)).State;

        var result = _reducer.Reduce(manual, new ViewPageLoaded(id, oldId, Page(1, null), _start));

        Assert.Same(manual, result.State);
    }

    [Fact]
    public void GivenRepeatedFailures_WhenTicking_ThenBackoffDoublesToTheCapAndResetsOnSuccess()
    {
        var state = Loaded(3);
        var now = _start + TimeSpan.FromSeconds(60);
        var waits = new List<double>();

        for (var round = 0; round < 6; round++)
        {
            var tick = _reducer.Reduce(state, new Tick(now));
            Assert.Single(tick.Effects);
            state = Fail(tick.State, JiraErrorKind.ServerError, now);
            var tab = state.Session.Tabs[0];
            waits.Add((tab.NextRefreshAt!.Value - now).TotalSeconds);
            Assert.Equal(round + 1, tab.Failures);
            Assert.Empty(_reducer.Reduce(state, new Tick(tab.NextRefreshAt.Value - TimeSpan.FromSeconds(1))).Effects);
            now = tab.NextRefreshAt.Value;
        }

        Assert.Equal([60d, 120d, 240d, 300d, 300d, 300d], waits);

        var retry = _reducer.Reduce(state, new Tick(now)).State;
        var success = _reducer.Reduce(
            retry,
            new ViewPageLoaded(retry.Session.Tabs[0].Id, retry.Session.Tabs[0].RequestId, Page(3, null), now));
        var reset = success.State.Session.Tabs[0];
        Assert.Equal(0, reset.Failures);
        Assert.Equal(now + TimeSpan.FromSeconds(60), reset.NextRefreshAt);
        Assert.Equal(new IdleState(), reset.Load);
    }

    [Fact]
    public void GivenFailedRefresh_WhenItFails_ThenRowsStayStaleAndTheErrorIsKept()
    {
        var tick = _reducer.Reduce(Loaded(3), new Tick(_start + TimeSpan.FromSeconds(60)));

        var failed = Fail(tick.State, JiraErrorKind.ServerError, _start + TimeSpan.FromSeconds(61));

        var tab = failed.Session.Tabs[0];
        Assert.Equal(3, tab.Rows.Count);
        Assert.True(tab.IsStale);
        var error = Assert.IsType<Failed>(tab.Load);
        Assert.Equal(JiraErrorKind.ServerError, error.Kind);
        Assert.Equal(LoadReason.Refresh, error.Reason);
    }

    [Fact]
    public void GivenUnauthorized_WhenRefreshFails_ThenLinkIsRejectedAndTicksStopUntilTheCheckSucceeds()
    {
        var tick = _reducer.Reduce(Loaded(3), new Tick(_start + TimeSpan.FromSeconds(60)));
        var failed = Fail(tick.State, JiraErrorKind.Unauthorized, _start + TimeSpan.FromSeconds(61));

        var later = _reducer.Reduce(failed, new Tick(_start + TimeSpan.FromHours(1)));

        Assert.IsType<Rejected>(failed.Session.Link);
        Assert.Empty(later.Effects);

        var checkedState = _reducer.Reduce(failed, new ConnectionChecked(_work, new JiraUser("id", "Me", null, true, null, null)));
        var resumed = _reducer.Reduce(checkedState.State, new Tick(_start + TimeSpan.FromHours(1)));

        Assert.IsType<Online>(checkedState.State.Session.Link);
        Assert.Single(resumed.Effects);
    }

    [Fact]
    public void GivenRejectedLink_WhenRefreshKeyIsPressed_ThenItStillRefreshesOnTheUserLane()
    {
        var tick = _reducer.Reduce(Loaded(3), new Tick(_start + TimeSpan.FromSeconds(60)));
        var failed = Fail(tick.State, JiraErrorKind.Unauthorized, _start + TimeSpan.FromSeconds(61));

        var result = _reducer.Reduce(failed, new KeyPressed("Space"));
        var refresh = _reducer.Reduce(result.State, new KeyPressed("r"));

        var effect = Assert.IsType<LoadViewPageEffect>(Assert.Single(refresh.Effects));
        Assert.False(effect.Background);
        Assert.Equal(LoadReason.Refresh, effect.Reason);
    }

    [Fact]
    public void GivenNetworkFailure_WhenRefreshFails_ThenLinkIsOfflineAndBackoffProbesRecovery()
    {
        var tick = _reducer.Reduce(Loaded(3), new Tick(_start + TimeSpan.FromSeconds(60)));
        var at = _start + TimeSpan.FromSeconds(61);
        var failed = Fail(tick.State, JiraErrorKind.Network, at);

        Assert.Equal(new Offline(at), failed.Session.Link);

        var probe = _reducer.Reduce(failed, new Tick(at + TimeSpan.FromSeconds(60)));
        var recovered = _reducer.Reduce(
            probe.State,
            new ViewPageLoaded(
                failed.Session.Tabs[0].Id,
                probe.State.Session.Tabs[0].RequestId,
                Page(3, null),
                at + TimeSpan.FromSeconds(60)));

        Assert.Single(probe.Effects);
        Assert.IsType<Online>(recovered.State.Session.Link);
    }

    [Fact]
    public void GivenRateLimitWait_WhenTicking_ThenNoRefreshStartsUntilItEnds()
    {
        var state = Loaded(3);
        var until = _start + TimeSpan.FromSeconds(120);
        var limited = _reducer.Reduce(state, new LinkChanged(new RateLimited(until))).State;

        var during = _reducer.Reduce(limited, new Tick(_start + TimeSpan.FromSeconds(100)));
        var passed = _reducer.Reduce(limited, new Tick(until));
        var ended = _reducer.Reduce(limited, new LinkChanged(new Online())).State;
        var after = _reducer.Reduce(ended, new Tick(_start + TimeSpan.FromSeconds(100)));

        Assert.Empty(during.Effects);
        Assert.Single(passed.Effects);
        Assert.IsType<Online>(ended.Session.Link);
        Assert.Single(after.Effects);
    }

    [Fact]
    public void GivenOfflineLink_WhenTheRateLimitWaitEnds_ThenLinkStaysOffline()
    {
        var offline = Loaded(1) with { };
        var state = offline with { Session = offline.Session with { Link = new Offline(_start) } };

        var result = _reducer.Reduce(state, new LinkChanged(new Online()));

        Assert.Same(state, result.State);
    }

    [Fact]
    public void GivenTabThatLoadedMorePages_WhenTicking_ThenNoAutomaticRefreshButTheKeyRefreshes()
    {
        var state = LoadedWith(Page(100, new PageCursor("n2")));
        var tab = state.Session.Tabs[0];
        var more = _reducer.Reduce(state, new NextPageRequested(tab.Id));
        var withTwo = _reducer.Reduce(
            more.State,
            new ViewPageLoaded(tab.Id, more.State.Session.Tabs[0].RequestId, Page(50, null, 101), _start));
        var tick = _reducer.Reduce(withTwo.State, new Tick(_start + TimeSpan.FromMinutes(5)));
        var manual = _reducer.Reduce(withTwo.State, new RefreshRequested(tab.Id));

        Assert.Equal(150, withTwo.State.Session.Tabs[0].Rows.Count);
        Assert.Empty(tick.Effects);
        Assert.False(Assert.IsType<LoadViewPageEffect>(Assert.Single(manual.Effects)).Background);
    }

    [Fact]
    public void GivenDoneCutoffSetting_WhenTickingAndRefreshingAndPaging_ThenEveryLoadCarriesIt()
    {
        var settings = new AppSettings(DoneCutoffDays: 7);
        var state = LoadedWith(Page(100, new PageCursor("n2"))) with { Settings = settings };
        var tab = state.Session.Tabs[0];

        var tick = _reducer.Reduce(state, new Tick(_start + TimeSpan.FromSeconds(60)));
        var manual = _reducer.Reduce(state, new RefreshRequested(tab.Id));
        var next = _reducer.Reduce(state, new NextPageRequested(tab.Id));

        foreach (var result in new[] { tick, manual, next })
        {
            Assert.Equal(7, Assert.IsType<LoadViewPageEffect>(Assert.Single(result.Effects)).DoneCutoffDays);
        }
    }

    [Fact]
    public void GivenManualRefresh_WhenALoadIsInFlight_ThenNothingStarts()
    {
        var tick = _reducer.Reduce(Loaded(3), new Tick(_start + TimeSpan.FromSeconds(60)));

        var result = _reducer.Reduce(tick.State, new RefreshRequested(tick.State.Session.Tabs[0].Id));

        Assert.Same(tick.State, result.State);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenNoTab_WhenRefreshKeyIsPressed_ThenStatusLineSaysSo()
    {
        var state = Start() with { Mode = InputMode.Normal };

        var pending = _reducer.Reduce(state, new KeyPressed("Space")).State;
        var result = _reducer.Reduce(pending, new KeyPressed("r"));

        Assert.Equal("No view to refresh.", result.State.Message);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenFailedNextPage_WhenRefreshKeyIsPressed_ThenThatPageIsRetriedAndRowsStay()
    {
        var state = LoadedWith(Page(100, new PageCursor("n2")));
        var tab = state.Session.Tabs[0];
        var next = _reducer.Reduce(state, new NextPageRequested(tab.Id));
        var failed = Fail(next.State, JiraErrorKind.ServerError, _start);

        var result = _reducer.Reduce(failed, new RefreshRequested(tab.Id));

        var effect = Assert.IsType<LoadViewPageEffect>(Assert.Single(result.Effects));
        Assert.Equal(LoadReason.NextPage, effect.Reason);
        Assert.Equal("n2", effect.Cursor!.Token);
        Assert.False(effect.Background);
        Assert.Equal(100, result.State.Session.Tabs[0].Rows.Count);
        Assert.Equal(0, failed.Session.Tabs[0].Failures);
    }

    [Fact]
    public void GivenFailedNextPage_WhenTicking_ThenNoAutomaticRefreshReplacesTheRows()
    {
        var state = LoadedWith(Page(100, new PageCursor("n2")));
        var next = _reducer.Reduce(state, new NextPageRequested(state.Session.Tabs[0].Id));
        var failed = Fail(next.State, JiraErrorKind.ServerError, _start);

        var result = _reducer.Reduce(failed, new Tick(_start + TimeSpan.FromHours(1)));

        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenAppendedPageWithKeysAlreadyLoaded_WhenPagesShifted_ThenDuplicatesAreDropped()
    {
        var state = LoadedWith(Page(3, new PageCursor("n2")));
        var tab = state.Session.Tabs[0];
        var next = _reducer.Reduce(state, new NextPageRequested(tab.Id));
        var page = new IssuePage([Issue(3), Issue(4), Issue(2), Issue(5)], null, null);

        var result = _reducer.Reduce(
            next.State,
            new ViewPageLoaded(tab.Id, next.State.Session.Tabs[0].RequestId, page, _start));

        var rows = result.State.Session.Tabs[0].Rows;
        Assert.Equal(["A-1", "A-2", "A-3", "A-4", "A-5"], rows.Select(issue => issue.Key));
    }

    [Fact]
    public void GivenPageExpiredOnNextPage_WhenItFails_ThenTheTabRestartsAtTheFirstPageAsRetry()
    {
        var state = LoadedWith(Page(100, new PageCursor("n2")));
        var tab = state.Session.Tabs[0];
        var next = _reducer.Reduce(state, new NextPageRequested(tab.Id));
        var error = new JiraError(JiraErrorKind.PageExpired, 400, "expired");

        var result = _reducer.Reduce(
            next.State,
            new ViewLoadFailed(tab.Id, next.State.Session.Tabs[0].RequestId, error, _start));

        var effect = Assert.IsType<LoadViewPageEffect>(Assert.Single(result.Effects));
        Assert.Equal(LoadReason.Retry, effect.Reason);
        Assert.Null(effect.Cursor);
        Assert.Equal(100, result.State.Session.Tabs[0].Rows.Count);
    }

    [Fact]
    public void GivenBoardTab_WhenRefreshedByTheClock_ThenOldRowsStayUntilTheLastPageThenAllSwapInAtOnce()
    {
        var state = LoadedBoard();
        var id = state.Session.Tabs[0].Id;
        var tick = _reducer.Reduce(state, new Tick(_start + TimeSpan.FromSeconds(60)));
        var first = Assert.IsType<LoadViewPageEffect>(Assert.Single(tick.Effects));

        var gathering = _reducer.Reduce(
            tick.State,
            new ViewPageLoaded(id, first.RequestId, Page(2, new PageCursor("r2"), 10), _start));
        var second = Assert.IsType<LoadViewPageEffect>(Assert.Single(gathering.Effects));
        var midway = gathering.State.Session.Tabs[0];
        var swapped = _reducer.Reduce(
            gathering.State,
            new ViewPageLoaded(id, second.RequestId, Page(2, null, 12), _start + TimeSpan.FromSeconds(62)));

        Assert.True(first.Background);
        Assert.True(second.Background);
        Assert.Equal("r2", second.Cursor!.Token);
        Assert.Equal(LoadReason.Refresh, second.Reason);
        Assert.Equal(["A-1", "A-2", "A-3"], midway.Rows.Select(issue => issue.Key));
        Assert.Equal(2, midway.PendingRows!.Count);
        Assert.Equal(["A-10", "A-11", "A-12", "A-13"], swapped.State.Session.Tabs[0].Rows.Select(issue => issue.Key));
        Assert.Null(swapped.State.Session.Tabs[0].PendingRows);
        Assert.Equal(new IdleState(), swapped.State.Session.Tabs[0].Load);
    }

    [Fact]
    public void GivenBoardRefreshGathering_WhenALaterPageFails_ThenOldRowsStayAndPendingRowsAreDropped()
    {
        var state = LoadedBoard();
        var id = state.Session.Tabs[0].Id;
        var tick = _reducer.Reduce(state, new Tick(_start + TimeSpan.FromSeconds(60)));
        var gathering = _reducer.Reduce(
            tick.State,
            new ViewPageLoaded(id, tick.State.Session.Tabs[0].RequestId, Page(2, new PageCursor("r2"), 10), _start));

        var failed = Fail(gathering.State, JiraErrorKind.ServerError, _start + TimeSpan.FromSeconds(62));

        var tab = failed.Session.Tabs[0];
        Assert.Equal(["A-1", "A-2", "A-3"], tab.Rows.Select(issue => issue.Key));
        Assert.Null(tab.PendingRows);
        Assert.True(tab.IsStale);
        Assert.Equal(1, tab.Failures);
    }

    [Fact]
    public void GivenBoardOpenFailedBeforeItsConfiguration_WhenTheClockRefreshes_ThenItRunsOnTheUserLane()
    {
        var definition = new ViewDefinition(ViewSourceKind.Board, "4", ViewLayout.Columns, "Board");
        var opened = _reducer.Reduce(Start(), new ViewOpenRequested(definition)).State;
        var failed = Fail(opened, JiraErrorKind.ServerError, _start);

        var tick = _reducer.Reduce(failed, new Tick(_start + TimeSpan.FromSeconds(60)));

        var effect = Assert.IsType<LoadViewPageEffect>(Assert.Single(tick.Effects));
        Assert.False(effect.Background);
        Assert.Equal(LoadReason.Refresh, effect.Reason);
    }

    [Fact]
    public void GivenRejectedLink_WhenARateLimitWaitStartsAndEnds_ThenTheLinkStaysRejected()
    {
        var tick = _reducer.Reduce(Loaded(3), new Tick(_start + TimeSpan.FromSeconds(60)));
        var state = Fail(tick.State, JiraErrorKind.Unauthorized, _start + TimeSpan.FromSeconds(61));

        var waiting = _reducer.Reduce(state, new LinkChanged(new RateLimited(_start + TimeSpan.FromSeconds(30))));
        var ended = _reducer.Reduce(waiting.State, new LinkChanged(new Online()));

        Assert.IsType<Rejected>(ended.State.Session.Link);
    }

    [Fact]
    public void GivenBoardTab_WhenRefreshedByHand_ThenTheEffectIsOnTheUserLane()
    {
        var state = LoadedBoard();

        var result = _reducer.Reduce(state, new RefreshRequested(state.Session.Tabs[0].Id));

        var effect = Assert.IsType<LoadViewPageEffect>(Assert.Single(result.Effects));
        Assert.False(effect.Background);
        Assert.Equal(LoadReason.Refresh, effect.Reason);
        Assert.Equal(new Loading(LoadReason.Refresh), result.State.Session.Tabs[0].Load);
    }

    [Fact]
    public void GivenSchedule_WhenAskedForBackoff_ThenItDoublesFromSixtySecondsUpToTheCap()
    {
        Assert.Equal(60, RefreshSchedule.Backoff(1).TotalSeconds);
        Assert.Equal(120, RefreshSchedule.Backoff(2).TotalSeconds);
        Assert.Equal(240, RefreshSchedule.Backoff(3).TotalSeconds);
        Assert.Equal(300, RefreshSchedule.Backoff(4).TotalSeconds);
        Assert.Equal(300, RefreshSchedule.Backoff(int.MaxValue).TotalSeconds);
        Assert.Equal(60, RefreshSchedule.Backoff(0).TotalSeconds);
    }

    private static AppState Start()
    {
        var profiles = ProfileCatalog.Empty.With(_work);

        return AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
    }

    private static IssuePage Page(int count, PageCursor? next, int firstNumber = 1)
    {
        var items = Enumerable.Range(firstNumber, count).Select(Issue).ToArray();

        return new IssuePage(items, next, null);
    }

    private static IssueSummary Issue(int number)
    {
        var status = new IssueStatus("1", "To Do", "new");
        var type = new IssueTypeSummary("10", "Task", 0, false);

        return new IssueSummary(
            number.ToString(CultureInfo.InvariantCulture),
            $"A-{number}",
            $"Issue {number}",
            status,
            type,
            null,
            null,
            [],
            null,
            null,
            "A");
    }

    private AppState Loaded(int rows)
    {
        return LoadedWith(Page(rows, null));
    }

    private AppState LoadedWith(IssuePage page)
    {
        var opened = Open(Start(), "project = A");

        return Reply(opened, opened.Session.Tabs[0], page, _start);
    }

    private AppState LoadedBoard()
    {
        var definition = new ViewDefinition(ViewSourceKind.Board, "4", ViewLayout.Columns, "Board");
        var opened = _reducer.Reduce(Start(), new ViewOpenRequested(definition)).State;
        var tab = opened.Session.Tabs[0];
        var configured = _reducer.Reduce(opened, new BoardConfigurationLoaded(tab.Id, tab.RequestId, _configuration)).State;
        var first = _reducer.Reduce(configured, new ViewPageLoaded(tab.Id, tab.RequestId, Page(2, new PageCursor("n2")), _start));
        var next = Assert.IsType<LoadViewPageEffect>(Assert.Single(first.Effects.OfType<LoadViewPageEffect>()));

        return _reducer.Reduce(first.State, new ViewPageLoaded(tab.Id, next.RequestId, Page(1, null, 3), _start)).State;
    }

    private AppState Open(AppState state, string jql)
    {
        var definition = new ViewDefinition(ViewSourceKind.Jql, jql, ViewLayout.List, jql);

        return _reducer.Reduce(state, new ViewOpenRequested(definition)).State;
    }

    private AppState Reply(AppState state, TabState tab, IssuePage page, DateTimeOffset at)
    {
        var current = state.Session.Tabs.Single(candidate => candidate.Id == tab.Id);

        return _reducer.Reduce(state, new ViewPageLoaded(tab.Id, current.RequestId, page, at)).State;
    }

    private AppState Fail(AppState state, JiraErrorKind kind, DateTimeOffset at)
    {
        var tab = state.Session.Tabs.Single(candidate => candidate.Id == state.Session.Active);
        var error = new JiraError(kind, null, "failed");

        return _reducer.Reduce(state, new ViewLoadFailed(tab.Id, tab.RequestId, error, at)).State;
    }
}
