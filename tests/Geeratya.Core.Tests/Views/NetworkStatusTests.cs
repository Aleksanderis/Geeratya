using System.Globalization;
using Geeratya.Core.Cache;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Tests.Views;

[Trait("Os", "Independent")]
public class NetworkStatusTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenLoadedTab_WhenNothingIsWrong_ThenTheStatusIsFreshAndThereIsNoBanner()
    {
        var state = Loaded();

        Assert.Equal(NetworkStatusKind.Fresh, TabStatus.Of(state.Session, _start).Kind);
        Assert.Null(NetworkBanner.Line(state, _start, 80));
        Assert.Equal(string.Empty, NetworkBanner.StatusWord(TabStatus.Fresh, _start));
    }

    [Fact]
    public void GivenNoTab_WhenAskingForTheBanner_ThenThereIsNone()
    {
        Assert.Null(NetworkBanner.Line(Start(), _start, 80));
        Assert.Equal(NetworkStatusKind.Fresh, TabStatus.Of(Start().Session, _start).Kind);
    }

    [Fact]
    public void GivenLoadedTab_WhenTheNetworkFails_ThenRowsStayAndTheTabIsOfflineAndStale()
    {
        var failed = Fail(Loaded(), JiraErrorKind.Network, _start + TimeSpan.FromSeconds(61));
        var tab = failed.Session.Tabs[0];

        var status = TabStatus.Of(failed.Session, _start + TimeSpan.FromSeconds(62));

        Assert.Equal(NetworkStatusKind.Offline, status.Kind);
        Assert.Equal(3, tab.Rows.Count);
        Assert.True(tab.IsStale);
        Assert.IsType<Offline>(failed.Session.Link);
    }

    [Theory]
    [InlineData(JiraErrorKind.Network)]
    [InlineData(JiraErrorKind.Timeout)]
    [InlineData(JiraErrorKind.HostRefused)]
    public void GivenUnreachableKinds_WhenTheLoadFails_ThenTheStatusIsOffline(JiraErrorKind kind)
    {
        var failed = Fail(Loaded(), kind, _start);

        Assert.Equal(NetworkStatusKind.Offline, TabStatus.Of(failed.Session, _start).Kind);
    }

    [Fact]
    public void GivenOfflineTab_WhenTheNextRefreshAnswers_ThenTheStateIsClearedAndTheRowsAreFresh()
    {
        var failed = Fail(Loaded(), JiraErrorKind.Network, _start + TimeSpan.FromSeconds(61));
        var due = _start + TimeSpan.FromSeconds(61) + RefreshSchedule.Backoff(1);
        var refreshing = _reducer.Reduce(failed, new Tick(due)).State;

        var recovered = Reply(refreshing, 3, due);

        Assert.IsType<Online>(recovered.Session.Link);
        Assert.False(recovered.Session.Tabs[0].IsStale);
        Assert.Equal(NetworkStatusKind.Fresh, TabStatus.Of(recovered.Session, due).Kind);
    }

    [Fact]
    public void GivenOfflineStartWithCache_WhenTheOpenFails_ThenCachedRowsStayMarkedStale()
    {
        var opened = Open(Start());
        var tab = opened.Session.Tabs[0];
        var cached = new ViewCacheLoaded(
            tab.Id,
            tab.RequestId,
            new CachedView(Page(2, null).Items, null, _start - TimeSpan.FromMinutes(23)));
        var withCache = _reducer.Reduce(opened, cached).State;

        var failed = Fail(withCache, JiraErrorKind.Network, _start);

        var status = TabStatus.Of(failed.Session, _start);
        var banner = NetworkBanner.Line(failed, _start, 120)!;
        Assert.Equal(NetworkStatusKind.Offline, status.Kind);
        Assert.Equal(2, failed.Session.Tabs[0].Rows.Count);
        Assert.Contains("Data is 23 min old.", banner.PlainText);
    }

    [Fact]
    public void GivenOfflineStartWithoutCache_WhenTheOpenFails_ThenThereAreNoRowsAndTheBannerOmitsTheData()
    {
        var failed = Fail(Open(Start()), JiraErrorKind.Network, _start);

        var banner = NetworkBanner.Line(failed, _start, 120)!;

        Assert.Empty(failed.Session.Tabs[0].Rows);
        Assert.DoesNotContain("Data is", banner.PlainText);
        Assert.Contains("Cannot reach work.atlassian.net.", banner.PlainText);
    }

    [Fact]
    public void GivenRateLimitWait_WhenLinkChanges_ThenTheStatusCountsDownAndEndsWithTheWait()
    {
        var until = _start + TimeSpan.FromSeconds(25);
        var limited = _reducer.Reduce(Loaded(), new LinkChanged(new RateLimited(until))).State;

        var during = TabStatus.Of(limited.Session, _start);
        var after = TabStatus.Of(limited.Session, until);

        Assert.Equal(NetworkStatusKind.RateLimited, during.Kind);
        Assert.Equal(until, during.Until);
        Assert.Equal("rate limited 25s", NetworkBanner.StatusWord(during, _start));
        Assert.Equal("rate limited 10s", NetworkBanner.StatusWord(during, until - TimeSpan.FromSeconds(10)));
        Assert.Equal(NetworkStatusKind.Fresh, after.Kind);
    }

    [Fact]
    public void GivenRateLimitWait_WhenLinkReturnsOnline_ThenTheStateIsCleared()
    {
        var limited = _reducer.Reduce(Loaded(), new LinkChanged(new RateLimited(_start + TimeSpan.FromSeconds(25)))).State;

        var online = _reducer.Reduce(limited, new LinkChanged(new Online())).State;

        Assert.Equal(NetworkStatusKind.Fresh, TabStatus.Of(online.Session, _start).Kind);
    }

    [Fact]
    public void GivenRateLimitWait_WhenTicking_ThenNoRefreshStartsUntilTheWaitEnds()
    {
        var until = _start + TimeSpan.FromSeconds(90);
        var limited = _reducer.Reduce(Loaded(), new LinkChanged(new RateLimited(until))).State;

        var during = _reducer.Reduce(limited, new Tick(_start + TimeSpan.FromSeconds(70)));
        var after = _reducer.Reduce(limited, new Tick(until));

        Assert.Empty(during.Effects);
        Assert.Single(after.Effects);
    }

    [Fact]
    public void GivenUnauthorizedReply_WhenTheLoadFails_ThenTheStatusIsARejectedTokenError()
    {
        var failed = Fail(Loaded(), JiraErrorKind.Unauthorized, _start + TimeSpan.FromSeconds(61));

        var status = TabStatus.Of(failed.Session, _start + TimeSpan.FromSeconds(62));
        var banner = NetworkBanner.Line(failed, _start, 120)!;

        Assert.Equal(NetworkStatusKind.Error, status.Kind);
        Assert.True(status.CredentialsRejected);
        Assert.Equal(TextRole.Error, banner.Spans[0].Role);
        Assert.Contains("Jira rejected the token (401).", banner.PlainText);
        Assert.Contains(":profile edit work and type the token again", banner.PlainText);
        Assert.Equal(3, failed.Session.Tabs[0].Rows.Count);
    }

    [Fact]
    public void GivenForbiddenReply_WhenTheLoadFails_ThenTheBannerSaysNotFoundOrNoAccess()
    {
        var failed = Fail(Loaded(), JiraErrorKind.Forbidden, _start);

        var banner = NetworkBanner.Line(failed, _start, 120)!;

        Assert.Contains("Not found, or you have no access to it.", banner.PlainText);
        Assert.Equal(TextRole.Error, banner.Spans[0].Role);
    }

    [Fact]
    public void GivenServerError_WhenTheLoadFails_ThenTheBannerNamesTheStatusAndTheRetryKey()
    {
        var failed = Fail(Loaded(), JiraErrorKind.ServerError, _start, 503);

        var banner = NetworkBanner.Line(failed, _start, 120)!;

        Assert.Contains("Jira returned an error (503). <space>r tries again.", banner.PlainText);
    }

    [Fact]
    public void GivenCachedRowsWhileLoading_WhenAskingForTheBanner_ThenItIsStaleAndRefreshing()
    {
        var opened = Open(Start());
        var tab = opened.Session.Tabs[0];
        var cached = new ViewCacheLoaded(
            tab.Id,
            tab.RequestId,
            new CachedView(Page(2, null).Items, null, _start - TimeSpan.FromMinutes(23)));
        var state = _reducer.Reduce(opened, cached).State;

        var banner = NetworkBanner.Line(state, _start, 120)!;

        Assert.Equal(TextRole.Stale, banner.Spans[0].Role);
        Assert.Contains("Data is 23 min old. Refreshing…", banner.PlainText);
    }

    [Fact]
    public void GivenFirstLoadWithoutCache_WhenAskingForTheStatus_ThenItIsLoadingWithoutABanner()
    {
        var opened = Open(Start());

        Assert.Equal(NetworkStatusKind.Loading, TabStatus.Of(opened.Session, _start).Kind);
        Assert.Null(NetworkBanner.Line(opened, _start, 80));
    }

    [Fact]
    public void GivenBackgroundRefresh_WhenAskingForTheStatus_ThenItIsNotAnnounced()
    {
        var refreshing = _reducer.Reduce(Loaded(), new Tick(_start + TimeSpan.FromSeconds(61))).State;

        Assert.IsType<Loading>(refreshing.Session.Tabs[0].Load);
        Assert.Equal(NetworkStatusKind.Fresh, TabStatus.Of(refreshing.Session, _start).Kind);
    }

    [Fact]
    public void GivenOfflineTab_WhenBuildingTheBanner_ThenItNamesTheDataAgeTheRetryAndTheKey()
    {
        var failedAt = _start + TimeSpan.FromSeconds(61);
        var failed = Fail(Loaded(), JiraErrorKind.Network, failedAt);
        var now = failedAt + TimeSpan.FromSeconds(52);

        var wide = NetworkBanner.Line(failed, now, 120)!;
        var compact = NetworkBanner.Line(failed, now, 40)!;

        Assert.Equal(
            " OFFLINE  Cannot reach work.atlassian.net. Retrying in 8 s, <space>r retries now. Data is 1 min old.",
            wide.PlainText);
        Assert.Equal(TextRole.Offline, wide.Spans[0].Role);
        Assert.Equal(" OFFLINE  Data 1 min old", compact.PlainText);
    }

    [Fact]
    public void GivenOfflineTabWhoseRetryIsInFlight_WhenBuildingTheBanner_ThenItSaysRetryingNow()
    {
        var failedAt = _start + TimeSpan.FromSeconds(61);
        var failed = Fail(Loaded(), JiraErrorKind.Network, failedAt);
        var retrying = _reducer.Reduce(failed, new RefreshRequested(failed.Session.Tabs[0].Id)).State;

        var banner = NetworkBanner.Line(retrying, failedAt, 120)!;

        Assert.Equal(NetworkStatusKind.Offline, TabStatus.Of(retrying.Session, failedAt).Kind);
        Assert.Contains("Retrying now. Data is", banner.PlainText);
    }

    [Fact]
    public void GivenTimeout_WhenBuildingTheBanner_ThenItSaysTheHostDidNotAnswer()
    {
        var failed = Fail(Loaded(), JiraErrorKind.Timeout, _start);

        var banner = NetworkBanner.Line(failed, _start, 120)!;

        Assert.Contains("work.atlassian.net did not answer in time.", banner.PlainText);
    }

    [Fact]
    public void GivenRateLimit_WhenBuildingTheBanner_ThenItCountsTheWait()
    {
        var limited = _reducer.Reduce(Loaded(), new LinkChanged(new RateLimited(_start + TimeSpan.FromSeconds(25)))).State;

        var wide = NetworkBanner.Line(limited, _start, 120)!;
        var compact = NetworkBanner.Line(limited, _start, 40)!;

        Assert.Equal(" RATE LIMITED  Waiting for the Jira rate limit. Retrying in 25 s.", wide.PlainText);
        Assert.Equal(" RATE LIMITED  Retrying in 25 s", compact.PlainText);
        Assert.Equal(TextRole.RateLimited, wide.Spans[0].Role);
    }

    [Fact]
    public void GivenRejectedToken_WhenBuildingTheCompactBanner_ThenItKeepsTheHint()
    {
        var failed = Fail(Loaded(), JiraErrorKind.Unauthorized, _start);

        var compact = NetworkBanner.Line(failed, _start, 40)!;

        Assert.Equal(" ERROR  Token rejected. See :profile", compact.PlainText);
        Assert.True(compact.PlainText.Length <= 40);
    }

    [Fact]
    public void GivenRejectedLinkWithAHealthyTab_WhenAskingForTheStatus_ThenTheWholeSiteShowsTheError()
    {
        var rejected = _reducer.Reduce(Loaded(), new LinkChanged(new Rejected())).State;

        var status = TabStatus.Of(rejected.Session, _start);

        Assert.Equal(NetworkStatusKind.Error, status.Kind);
        Assert.True(status.CredentialsRejected);
    }

    [Fact]
    public void GivenRejectedLinkAfterA401_WhenAnotherTabAsks_ThenItIsAnErrorToo()
    {
        var failed = Fail(Loaded(), JiraErrorKind.Unauthorized, _start);
        var other = failed.Session.Tabs[0] with { Load = LoadState.Idle };

        var status = TabStatus.Of(other, failed.Session.Link, _start);

        Assert.Equal(NetworkStatusKind.Error, status.Kind);
        Assert.True(status.CredentialsRejected);
    }

    [Fact]
    public void GivenTransportWaitEvents_WhenMappingTheLink_ThenAWaitIsRateLimitedAndNoWaitIsOnline()
    {
        var until = _start + TimeSpan.FromSeconds(30);

        Assert.Equal(new RateLimited(until), SiteLink.FromRateLimit(until));
        Assert.Equal(new Online(), SiteLink.FromRateLimit(null));
    }

    [Theory]
    [InlineData(0, "0 s")]
    [InlineData(45, "45 s")]
    [InlineData(60, "1 min")]
    [InlineData(23 * 60, "23 min")]
    [InlineData(3600, "1 h")]
    [InlineData(47 * 3600, "47 h")]
    [InlineData(2 * 24 * 3600, "2 d")]
    public void GivenAnAge_WhenFormatting_ThenTheUnitFits(int seconds, string expected)
    {
        Assert.Equal(expected, NetworkBanner.Age(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void GivenEachKind_WhenAskingIfItShowsTime_ThenOnlyCountingStatesDo()
    {
        Assert.True(new TabStatus(NetworkStatusKind.Offline).ShowsTime);
        Assert.True(new TabStatus(NetworkStatusKind.RateLimited).ShowsTime);
        Assert.True(new TabStatus(NetworkStatusKind.Stale).ShowsTime);
        Assert.False(new TabStatus(NetworkStatusKind.Error).ShowsTime);
        Assert.False(new TabStatus(NetworkStatusKind.Loading).ShowsTime);
        Assert.False(TabStatus.Fresh.ShowsTime);
    }

    private static AppState Start()
    {
        var profiles = ProfileCatalog.Empty.With(_work);

        return AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
    }

    private static IssuePage Page(int count, PageCursor? next)
    {
        return new IssuePage(Enumerable.Range(1, count).Select(Issue).ToArray(), next, null);
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

    private AppState Open(AppState state)
    {
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");

        return _reducer.Reduce(state, new ViewOpenRequested(definition)).State;
    }

    private AppState Loaded()
    {
        return Reply(Open(Start()), 3, _start);
    }

    private AppState Reply(AppState state, int rows, DateTimeOffset at)
    {
        var tab = state.Session.Tabs[0];

        return _reducer.Reduce(state, new ViewPageLoaded(tab.Id, tab.RequestId, Page(rows, null), at)).State;
    }

    private AppState Fail(AppState state, JiraErrorKind kind, DateTimeOffset at, int? status = null)
    {
        var refreshing = state.Session.Tabs[0].Load is Loading
            ? state
            : _reducer.Reduce(state, new RefreshRequested(state.Session.Tabs[0].Id)).State;
        var tab = refreshing.Session.Tabs[0];
        var error = new JiraError(kind, status, "failed");

        return _reducer.Reduce(refreshing, new ViewLoadFailed(tab.Id, tab.RequestId, error, at)).State;
    }
}
