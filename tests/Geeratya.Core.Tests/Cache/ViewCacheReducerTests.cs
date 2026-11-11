using Geeratya.Core.Cache;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;
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
    public void GivenLoadedView_WhenARefreshBringsTheSameIssues_ThenTheRowsKeepTheirReferenceAndNothingIsCached()
    {
        var loaded = LoadedWithUpdates(3, _saved);
        var before = loaded.Session.Tabs[0];

        var result = Refresh(loaded, ReplyItems(3, _saved));

        var tab = Assert.Single(result.State.Session.Tabs);
        Assert.Same(before.Rows, tab.Rows);
        Assert.Empty(result.Effects.OfType<WriteCacheEffect>());
        Assert.Equal(_now.AddMinutes(1), tab.LoadedAt);
        Assert.Equal(LoadState.Idle, tab.Load);
    }

    [Fact]
    public void GivenLoadedView_WhenARefreshBringsAnIssueUpdatedLater_ThenTheRowsAreReplacedAndCached()
    {
        var loaded = LoadedWithUpdates(3, _saved);
        var items = ReplyItems(3, _saved);
        items[1] = items[1] with { Updated = _saved.AddMinutes(5), Summary = "Edited" };

        var result = Refresh(loaded, items);

        var tab = result.State.Session.Tabs[0];
        Assert.Equal("Edited", tab.Rows[1].Summary);
        Assert.Single(result.Effects.OfType<WriteCacheEffect>());
    }

    [Fact]
    public void GivenLoadedView_WhenOnlyASubtaskStatusChangesWithTheSameUpdated_ThenTheRowsAreReplaced()
    {
        var done = new IssueStatus("3", "Done", "done");
        var open = new IssueSubtask("A-11", "Child", new IssueStatus("1", "To Do", "new"), "Sub-task");
        var held = ReplyItems(2, _saved);
        held[0] = held[0] with { Subtasks = [open] };
        var loaded = LoadedWith(held);
        var fresh = ReplyItems(2, _saved);
        fresh[0] = fresh[0] with { Subtasks = [open with { Status = done }] };

        var result = Refresh(loaded, fresh);

        Assert.Equal(done, result.State.Session.Tabs[0].Rows[0].Subtasks[0].Status);
        Assert.Single(result.Effects.OfType<WriteCacheEffect>());
    }

    [Fact]
    public void GivenLoadedView_WhenOnlyASprintStateChangesWithTheSameUpdated_ThenTheRowsAreReplaced()
    {
        var held = ReplyItems(2, _saved);
        held[0] = held[0] with { Sprints = [new IssueSprint(7, "Sprint 7", SprintState.Future)] };
        var loaded = LoadedWith(held);
        var fresh = ReplyItems(2, _saved);
        fresh[0] = fresh[0] with { Sprints = [new IssueSprint(7, "Sprint 7", SprintState.Active)] };

        var result = Refresh(loaded, fresh);

        Assert.Equal(SprintState.Active, result.State.Session.Tabs[0].Rows[0].Sprints[0].State);
        Assert.Single(result.Effects.OfType<WriteCacheEffect>());
    }

    [Fact]
    public void GivenRowsWithoutAnUpdatedTime_WhenARefreshBringsTheSameIssues_ThenTheyAreStillReplaced()
    {
        var loaded = LoadedWith([Issue(1), Issue(2)]);

        var result = Refresh(loaded, [Issue(1), Issue(2)]);

        Assert.Single(result.Effects.OfType<WriteCacheEffect>());
    }

    [Fact]
    public void GivenLoadedView_WhenARefreshBringsAnIssueMoreOrFewer_ThenTheRowsAreReplacedAndCached()
    {
        var loaded = LoadedWithUpdates(3, _saved);

        var result = Refresh(loaded, ReplyItems(2, _saved));

        Assert.Equal(2, result.State.Session.Tabs[0].Rows.Count);
        Assert.Single(result.Effects.OfType<WriteCacheEffect>());
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
        var result = _reducer.Reduce(AppState.Initial, new CacheProblem("cache broke"));

        Assert.Equal("cache broke", result.State.Message);
        Assert.Equal(["cache broke"], result.State.StartupProblems);
    }

    [Fact]
    public void GivenStartupProblem_WhenCacheProblemArrives_ThenBothAreListedAndTheNewOneShows()
    {
        var state = StatusMessages.AddStartupProblems(AppState.Initial, ["Keymap: bad"]);

        var result = _reducer.Reduce(state, new CacheProblem("cache broke"));

        Assert.Equal(["Keymap: bad", "cache broke"], result.State.StartupProblems);
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

    private ReduceResult Refresh(AppState state, IssueSummary[] items)
    {
        var requested = _reducer.Reduce(state, new RefreshRequested(state.Session.Tabs[0].Id));
        var tab = requested.State.Session.Tabs[0];
        var reply = new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage(items, null, null), _now.AddMinutes(1));

        return _reducer.Reduce(requested.State, reply);
    }

    private AppState LoadedWithUpdates(int count, DateTimeOffset updated)
    {
        return LoadedWith(ReplyItems(count, updated));
    }

    private AppState LoadedWith(IssueSummary[] items)
    {
        var opened = Open();
        var tab = opened.State.Session.Tabs[0];
        var reply = new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage(items, null, null), _now);

        return _reducer.Reduce(opened.State, reply).State;
    }

    private static IssueSummary[] ReplyItems(int count, DateTimeOffset updated)
    {
        return [.. Enumerable.Range(1, count).Select(number => Issue(number) with { Updated = updated })];
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
