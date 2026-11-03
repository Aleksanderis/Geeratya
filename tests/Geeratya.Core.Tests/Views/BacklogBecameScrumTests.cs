using Geeratya.Core.Cache;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Views;

[Trait("Os", "Independent")]
public class BacklogBecameScrumTests
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenLoadingPlainBacklog_WhenItBecameScrum_ThenTheTabTakesTheScrumBacklogSource()
    {
        var opened = Open(ViewSourceKind.Backlog);
        var tab = opened.Session.Tabs[0];

        var result = _reducer.Reduce(opened, new BacklogBecameScrum(tab.Id, tab.RequestId));

        Assert.Equal(ViewSourceKind.ScrumBacklog, result.State.Session.Tabs[0].Definition.Source);
    }

    [Fact]
    public void GivenStaleRequest_WhenBacklogBecameScrumArrives_ThenNothingChanges()
    {
        var opened = Open(ViewSourceKind.Backlog);
        var tab = opened.Session.Tabs[0];

        var result = _reducer.Reduce(opened, new BacklogBecameScrum(tab.Id, tab.RequestId + 100));

        Assert.Same(opened, result.State);
    }

    [Fact]
    public void GivenTabNoLongerLoading_WhenBacklogBecameScrumArrives_ThenNothingChanges()
    {
        var opened = Open(ViewSourceKind.Backlog);
        var tab = opened.Session.Tabs[0];
        var idle = opened with
        {
            Session = opened.Session with { Tabs = [tab with { Load = LoadState.Idle }] },
        };

        var result = _reducer.Reduce(idle, new BacklogBecameScrum(tab.Id, tab.RequestId));

        Assert.Same(idle, result.State);
    }

    [Fact]
    public void GivenOtherSource_WhenBacklogBecameScrumArrives_ThenNothingChanges()
    {
        var opened = Open(ViewSourceKind.Board);
        var tab = opened.Session.Tabs[0];

        var result = _reducer.Reduce(opened, new BacklogBecameScrum(tab.Id, tab.RequestId));

        Assert.Same(opened, result.State);
    }

    [Fact]
    public void GivenRowsCachedAsScrumBacklog_WhenPlainBacklogOpens_ThenTheTabBecomesScrumBacklogWithThem()
    {
        var opened = Open(ViewSourceKind.Backlog);
        var tab = opened.Session.Tabs[0];
        var view = new CachedView([Issue()], null, DateTimeOffset.UnixEpoch);

        var result = _reducer.Reduce(opened, new ViewCacheLoaded(tab.Id, tab.RequestId, view, true));

        var shown = result.State.Session.Tabs[0];
        Assert.Equal(ViewSourceKind.ScrumBacklog, shown.Definition.Source);
        Assert.Single(shown.Rows);
    }

    private static IssueSummary Issue()
    {
        var status = new IssueStatus("1", "To Do", "new");
        var type = new IssueTypeSummary("10", "Task", 0, false);

        return new IssueSummary("1", "A-1", "Issue 1", status, type, null, null, [], null, null, "A");
    }

    private AppState Open(ViewSourceKind source)
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var state = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
        var layout = source == ViewSourceKind.Board ? ViewLayout.Columns : ViewLayout.List;

        return _reducer.Reduce(state, new ViewOpenRequested(new ViewDefinition(source, "5", layout, "B"))).State;
    }
}
