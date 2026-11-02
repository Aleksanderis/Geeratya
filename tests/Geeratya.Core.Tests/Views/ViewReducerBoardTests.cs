using System.Globalization;
using Geeratya.Core.Cache;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Tests.Views;

[Trait("Os", "Independent")]
public class ViewReducerBoardTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
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

    [Theory]
    [InlineData(ViewSourceKind.Board)]
    [InlineData(ViewSourceKind.Backlog)]
    public void GivenBoardOrBacklogSource_WhenOpening_ThenTheFirstPageIsRequestedWithTheBoardId(ViewSourceKind source)
    {
        var result = Open(Start(), source);

        var effect = Assert.IsType<LoadViewPageEffect>(Assert.Single(result.Effects));
        Assert.Equal(source, effect.Definition.Source);
        Assert.Equal("4", effect.Definition.SourceValue);
        Assert.Null(effect.Cursor);
        Assert.Equal(LoadReason.Open, effect.Reason);
    }

    [Fact]
    public void GivenOpenBoardTab_WhenTheConfigurationArrives_ThenItIsKeptInTheTabWhileStillLoading()
    {
        var opened = Open(Start(), ViewSourceKind.Board);
        var tab = opened.State.Session.Tabs[0];

        var result = _reducer.Reduce(opened.State, new BoardConfigurationLoaded(tab.Id, tab.RequestId, _configuration));

        var updated = result.State.Session.Tabs[0];
        Assert.Equal(_configuration, updated.Board);
        Assert.Equal(new Loading(LoadReason.Open), updated.Load);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenOpenBoardTab_WhenAStaleConfigurationArrives_ThenItIsIgnored()
    {
        var opened = Open(Start(), ViewSourceKind.Board);
        var tab = opened.State.Session.Tabs[0];

        var stale = _reducer.Reduce(
            opened.State,
            new BoardConfigurationLoaded(tab.Id, tab.RequestId - 1, _configuration));
        var unknown = _reducer.Reduce(
            opened.State,
            new BoardConfigurationLoaded(new TabId(999), tab.RequestId, _configuration));

        Assert.Null(stale.State.Session.Tabs[0].Board);
        Assert.Same(opened.State, unknown.State);
    }

    [Fact]
    public void GivenBoardWithSeveralPages_WhenAPageArrives_ThenTheNextOneIsRequestedWithoutAnySelection()
    {
        var opened = Open(Start(), ViewSourceKind.Board);

        var first = Reply(opened, Page(100, PageCursor.Offset(100)));

        var effect = Assert.IsType<LoadViewPageEffect>(
            Assert.Single(first.Effects, candidate => candidate is LoadViewPageEffect));
        Assert.Equal(PageCursor.Offset(100), effect.Cursor);
        Assert.Equal(LoadReason.NextPage, effect.Reason);
        Assert.Equal(new Loading(LoadReason.NextPage), first.State.Session.Tabs[0].Load);
    }

    [Fact]
    public void GivenBoardWalkingPages_WhenTheLastPageArrives_ThenRowsAreAllThereAndNothingMoreIsRequested()
    {
        var opened = Open(Start(), ViewSourceKind.Backlog);
        var first = Reply(opened, Page(100, PageCursor.Offset(100)));
        var second = Reply(first, Page(100, PageCursor.Offset(200), 101));

        var last = Reply(second, Page(30, null, 201));

        var tab = last.State.Session.Tabs[0];
        Assert.Equal(230, tab.Rows.Count);
        Assert.Null(tab.Next);
        Assert.Equal(LoadState.Idle, tab.Load);
        Assert.DoesNotContain(last.Effects, effect => effect is LoadViewPageEffect);
    }

    [Fact]
    public void GivenBoardTabInTheBackground_WhenAPageArrives_ThenItKeepsLoadingTheRest()
    {
        var opened = Open(Start(), ViewSourceKind.Board);
        var boardTab = opened.State.Session.Tabs[0];
        var other = _reducer.Reduce(opened.State, new ViewOpenRequested(Definition("project = A")));

        var result = _reducer.Reduce(
            other.State,
            new ViewPageLoaded(boardTab.Id, boardTab.RequestId, Page(100, PageCursor.Offset(100)), _now));

        var effect = Assert.IsType<LoadViewPageEffect>(Assert.Single(result.Effects, e => e is LoadViewPageEffect));
        Assert.Equal(boardTab.Id, effect.Tab);
        Assert.NotEqual(boardTab.Id, result.State.Session.Active);
    }

    [Fact]
    public void GivenJqlTab_WhenAPageArrivesWithNoSelectionNearTheEnd_ThenNoNextPageIsRequested()
    {
        var opened = _reducer.Reduce(Start(), new ViewportChanged(10)).State;
        var jql = _reducer.Reduce(opened, new ViewOpenRequested(Definition("project = A")));

        var result = Reply(jql, Page(100, new PageCursor("token")));

        Assert.DoesNotContain(result.Effects, effect => effect is LoadViewPageEffect);
    }

    [Fact]
    public void GivenBoardWithMoreThanTheCap_WhenTheCapIsReached_ThenPagingStopsAndTheTabIsTruncated()
    {
        var opened = Open(Start(), ViewSourceKind.Board);

        var result = Reply(opened, Page(ViewPaging.MaxRowsPerTab, PageCursor.Offset(ViewPaging.MaxRowsPerTab)));

        var tab = result.State.Session.Tabs[0];
        Assert.True(tab.Truncated);
        Assert.Null(tab.Next);
        Assert.Equal(ViewPaging.MaxRowsPerTab, tab.Rows.Count);
        Assert.DoesNotContain(result.Effects, effect => effect is LoadViewPageEffect);
    }

    [Fact]
    public void GivenBoardWalkingPages_WhenAPageFails_ThenTheTabFailsKeepingRowsAndAsksForNothingMore()
    {
        var opened = Open(Start(), ViewSourceKind.Board);
        var first = Reply(opened, Page(100, PageCursor.Offset(100)));
        var tab = first.State.Session.Tabs[0];
        var error = new JiraError(JiraErrorKind.Timeout, null, "timed out");

        var failed = _reducer.Reduce(first.State, new ViewLoadFailed(tab.Id, tab.RequestId, error, _now));

        var after = failed.State.Session.Tabs[0];
        Assert.IsType<Failed>(after.Load);
        Assert.Equal(100, after.Rows.Count);
        Assert.True(after.IsStale);
        Assert.Empty(failed.Effects);
    }

    [Fact]
    public void GivenConfigurationFailure_WhenTheFailureArrives_ThenTheErrorShowsWithoutRowsOrConfiguration()
    {
        var opened = Open(Start(), ViewSourceKind.Board);
        var tab = opened.State.Session.Tabs[0];
        var error = new JiraError(JiraErrorKind.NotFound, 404, "No board");

        var failed = _reducer.Reduce(opened.State, new ViewLoadFailed(tab.Id, tab.RequestId, error, _now));

        var after = failed.State.Session.Tabs[0];
        var state = Assert.IsType<Failed>(after.Load);
        Assert.Equal(LoadErrorText.For(error), state.Message);
        Assert.Empty(after.Rows);
        Assert.Null(after.Board);
    }

    [Fact]
    public void GivenConfigurationAndFirstPage_WhenThePageArrives_ThenTheCacheWriteCarriesTheConfiguration()
    {
        var opened = Open(Start(), ViewSourceKind.Board);
        var tab = opened.State.Session.Tabs[0];
        var configured = _reducer.Reduce(
            opened.State,
            new BoardConfigurationLoaded(tab.Id, tab.RequestId, _configuration));

        var result = Reply(configured, Page(3, null));

        var write = Assert.IsType<WriteCacheEffect>(Assert.Single(result.Effects, e => e is WriteCacheEffect));
        Assert.Equal(_configuration, write.View.Board);
        Assert.Equal(_configuration, result.State.Session.Tabs[0].Board);
    }

    [Fact]
    public void GivenCachedBoardView_WhenItArrivesBeforeTheNetwork_ThenRowsAndConfigurationAreShownStale()
    {
        var opened = Open(Start(), ViewSourceKind.Board);
        var tab = opened.State.Session.Tabs[0];
        var cached = new CachedView(Page(3, null).Items, _configuration, _now);

        var result = _reducer.Reduce(opened.State, new ViewCacheLoaded(tab.Id, tab.RequestId, cached));

        var shown = result.State.Session.Tabs[0];
        Assert.True(shown.IsStale);
        Assert.Equal(_configuration, shown.Board);
        Assert.Equal(3, shown.Rows.Count);
    }

    private static AppState Start()
    {
        return AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(_work),
            Session = SiteSession.Empty(_work),
        };
    }

    private static ViewDefinition Definition(string jql)
    {
        return new ViewDefinition(ViewSourceKind.Jql, jql, ViewLayout.List, jql);
    }

    private static IssuePage Page(int count, PageCursor? next, int firstNumber = 1)
    {
        var items = Enumerable.Range(firstNumber, count).Select(Issue).ToArray();

        return new IssuePage(items, next, null);
    }

    private static IssueSummary Issue(int number)
    {
        return new IssueSummary(
            number.ToString(CultureInfo.InvariantCulture),
            $"A-{number}",
            $"Issue {number}",
            new IssueStatus("1", "Backlog", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
    }

    private ReduceResult Open(AppState state, ViewSourceKind source)
    {
        var definition = new ViewDefinition(source, "4", ViewLayout.Columns, "GCM board");

        return _reducer.Reduce(state, new ViewOpenRequested(definition));
    }

    /// <summary>Delivers a page for the current request of the active tab in <paramref name="previous"/>.</summary>
    private ReduceResult Reply(ReduceResult previous, IssuePage page)
    {
        var tab = previous.State.Session.Tabs.Single(candidate => candidate.Id == previous.State.Session.Active);

        return _reducer.Reduce(previous.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now));
    }
}
