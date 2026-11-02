using Geeratya.Core.Cache;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Tests.Cache;

[Trait("Os", "Independent")]
public class ViewCacheReducerTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _saved = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenOpeningTab_WhenCachedRowsArrive_ThenTheyAreShownStaleAndStillLoading()
    {
        var opened = Open();

        var result = _reducer.Reduce(opened.State, Cached(opened, 3));

        var tab = Assert.Single(result.State.Session.Tabs);
        Assert.Equal(["A-1", "A-2", "A-3"], tab.Rows.Select(row => row.Key));
        Assert.True(tab.IsStale);
        Assert.Equal(_saved, tab.LoadedAt);
        Assert.Equal(new Loading(LoadReason.Open), tab.Load);
        Assert.Equal(opened.State.Session.Tabs[0].RequestId, tab.RequestId);
        Assert.Equal("A-1", tab.SelectedKey);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenOpeningTab_WhenCachedRowsArrive_ThenKeyWidthCoversTheWidestCachedKey()
    {
        var opened = Open();

        var result = _reducer.Reduce(opened.State, Cached(opened, 3));

        Assert.Equal("A-1".Length, Assert.Single(result.State.Session.Tabs).KeyWidth);
        var wide = _reducer.Reduce(opened.State, Cached(opened, 12));
        Assert.Equal("A-12".Length, Assert.Single(wide.State.Session.Tabs).KeyWidth);
    }

    [Fact]
    public void GivenNetworkReplyAlreadyShown_WhenCachedRowsArriveLate_ThenTheyAreIgnored()
    {
        var opened = Open();
        var tab = opened.State.Session.Tabs[0];
        var fresh = _reducer.Reduce(opened.State, Reply(opened, 2));
        var late = new ViewCacheLoaded(tab.Id, tab.RequestId, CachedView(5));

        var result = _reducer.Reduce(fresh.State, late);

        Assert.Same(fresh.State, result.State);
    }

    [Fact]
    public void GivenSupersededRequest_WhenCachedRowsArrive_ThenTheyAreIgnored()
    {
        var opened = Open();
        var superseded = new ViewCacheLoaded(opened.State.Session.Tabs[0].Id, 999, CachedView(2));

        var result = _reducer.Reduce(opened.State, superseded);

        Assert.Same(opened.State, result.State);
    }

    [Fact]
    public void GivenClosedTab_WhenCachedRowsArrive_ThenNothingChanges()
    {
        var opened = Open();
        var tab = opened.State.Session.Tabs[0];
        var closed = _reducer.Reduce(opened.State, new TabClosed(tab.Id));

        var result = _reducer.Reduce(closed.State, new ViewCacheLoaded(tab.Id, tab.RequestId, CachedView(2)));

        Assert.Same(closed.State, result.State);
    }

    [Fact]
    public void GivenCachedRowsShown_WhenTheNetworkReplyMovesRows_ThenSelectionStaysOnItsIssueKeyAndStaleClears()
    {
        var opened = Open();
        var shown = _reducer.Reduce(opened.State, Cached(opened, 3));
        var tab = shown.State.Session.Tabs[0];
        var moved = _reducer.Reduce(shown.State, new SelectionMoved(tab.Id, 1));
        var reply = new ViewPageLoaded(tab.Id, tab.RequestId, Page(4, 0), _now);

        var result = _reducer.Reduce(moved.State, reply);

        var updated = result.State.Session.Tabs[0];
        Assert.Equal("A-2", updated.SelectedKey);
        Assert.Equal(["A-0", "A-1", "A-2", "A-3"], updated.Rows.Select(row => row.Key));
        Assert.Equal(2, updated.Selected);
        Assert.False(updated.IsStale);
        Assert.Equal(_now, updated.LoadedAt);
    }

    [Fact]
    public void GivenCachedRowsShown_WhenTheLoadFails_ThenRowsStayStaleWithTheError()
    {
        var opened = Open();
        var shown = _reducer.Reduce(opened.State, Cached(opened, 2));
        var tab = shown.State.Session.Tabs[0];
        var error = new JiraError(JiraErrorKind.Network, null, "offline");

        var result = _reducer.Reduce(shown.State, new ViewLoadFailed(tab.Id, tab.RequestId, error, _now));

        var failed = result.State.Session.Tabs[0];
        Assert.Equal(2, failed.Rows.Count);
        Assert.True(failed.IsStale);
        Assert.IsType<Failed>(failed.Load);
    }

    [Fact]
    public void GivenFirstPageArrives_WhenReduced_ThenItIsWrittenToTheCacheUnderTheViewKey()
    {
        var opened = Open();

        var result = _reducer.Reduce(opened.State, Reply(opened, 3));

        var write = Assert.Single(result.Effects.OfType<WriteCacheEffect>());
        Assert.Equal(new CacheKey(ViewSourceKind.Jql, "project = A"), write.Key);
        Assert.Equal(["A-1", "A-2", "A-3"], write.View.Rows.Select(row => row.Key));
        Assert.Equal(_now, write.View.SavedAt);
    }

    [Fact]
    public void GivenMoreThanOnePageOfRows_WhenTheFirstPageArrives_ThenOnlyOnePageIsWritten()
    {
        var opened = Open();

        var result = _reducer.Reduce(opened.State, Reply(opened, ViewPaging.PageSize + 20));

        var write = Assert.Single(result.Effects.OfType<WriteCacheEffect>());
        Assert.Equal(ViewPaging.PageSize, write.View.Rows.Count);
    }

    [Fact]
    public void GivenNextPageArrives_WhenReduced_ThenTheCacheIsNotWritten()
    {
        var opened = Open();
        var first = _reducer.Reduce(opened.State, Reply(opened, 3, new PageCursor("n2")));
        var next = _reducer.Reduce(first.State, new NextPageRequested(first.State.Session.Tabs[0].Id));
        var tab = next.State.Session.Tabs[0];

        var result = _reducer.Reduce(next.State, new ViewPageLoaded(tab.Id, tab.RequestId, Page(2, 4), _now));

        Assert.DoesNotContain(result.Effects, effect => effect is WriteCacheEffect);
    }

    [Fact]
    public void GivenFailedLoad_WhenReduced_ThenNothingIsWritten()
    {
        var opened = Open();
        var tab = opened.State.Session.Tabs[0];
        var error = new JiraError(JiraErrorKind.Network, null, "offline");

        var result = _reducer.Reduce(opened.State, new ViewLoadFailed(tab.Id, tab.RequestId, error, _now));

        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenWriteFailureNotice_WhenReduced_ThenTheStatusLineShowsIt()
    {
        var result = _reducer.Reduce(AppState.Initial, new CacheWriteFailed("cache broke"));

        Assert.Equal("cache broke", result.State.Message);
    }

    private static CachedView CachedView(int count)
    {
        return new CachedView(Page(count).Items, null, _saved);
    }

    private static ViewCacheLoaded Cached(ReduceResult opened, int count)
    {
        var tab = opened.State.Session.Tabs[0];

        return new ViewCacheLoaded(tab.Id, tab.RequestId, CachedView(count));
    }

    private static ViewPageLoaded Reply(ReduceResult opened, int count, PageCursor? next = null)
    {
        var tab = opened.State.Session.Tabs[0];

        return new ViewPageLoaded(tab.Id, tab.RequestId, Page(count, 1, next), _now);
    }

    private static IssuePage Page(int count, int first = 1, PageCursor? next = null)
    {
        var items = Enumerable.Range(first, count).Select(Issue).ToArray();

        return new IssuePage(items, next, null);
    }

    private static IssueSummary Issue(int number)
    {
        var status = new IssueStatus("1", "To Do", "new");
        var type = new IssueTypeSummary("10", "Task", 0, false);

        return new IssueSummary(
            $"{number}",
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

    private ReduceResult Open()
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var state = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "A");

        return _reducer.Reduce(state, new ViewOpenRequested(definition));
    }
}
