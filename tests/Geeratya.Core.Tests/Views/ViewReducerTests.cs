using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Tests.Views;

[Trait("Os", "Independent")]
public class ViewReducerTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenNoTabs_WhenOpeningAView_ThenTheTabIsActiveLoadingAndTheFirstPageIsRequested()
    {
        var result = Open(Start(), "project = A");

        var tab = Assert.Single(result.State.Session.Tabs);
        var effect = Assert.IsType<LoadViewPageEffect>(Assert.Single(result.Effects));
        Assert.Equal(tab.Id, result.State.Session.Active);
        Assert.Equal(new Loading(LoadReason.Open), tab.Load);
        Assert.Equal(tab.Id, effect.Tab);
        Assert.Equal(tab.RequestId, effect.RequestId);
        Assert.Null(effect.Cursor);
        Assert.Equal(LoadReason.Open, effect.Reason);
        Assert.False(effect.Background);
        Assert.Empty(tab.Rows);
    }

    [Fact]
    public void GivenOpenedTab_WhenTheFirstPageArrives_ThenRowsAreShownAndTheFirstIsSelected()
    {
        var opened = Open(Start(), "project = A");

        var loaded = Loaded(opened, Page(3, new PageCursor("n2")));

        var tab = Assert.Single(loaded.State.Session.Tabs);
        Assert.Equal(3, tab.Rows.Count);
        Assert.Equal(new PageCursor("n2"), tab.Next);
        Assert.Equal(LoadState.Idle, tab.Load);
        Assert.Equal(0, tab.RequestId);
        Assert.Equal(0, tab.Selected);
        Assert.Equal("A-1", tab.SelectedKey);
        Assert.Equal(_now, tab.LoadedAt);
        Assert.False(tab.IsStale);
    }

    [Fact]
    public void GivenLoadedTabWithNextPage_WhenRequestingAndReceivingIt_ThenRowsAreAppendedAndSelectionStays()
    {
        var first = Loaded(Open(Start(), "project = A"), Page(2, new PageCursor("n")));
        var id = first.State.Session.Tabs[0].Id;
        var moved = SetSelection(first.State, id, 1);

        var requested = _reducer.Reduce(moved, new NextPageRequested(id));
        var effect = Assert.IsType<LoadViewPageEffect>(Assert.Single(requested.Effects));
        var appended = Reply(requested, Page(2, null, 3));

        var tab = Assert.Single(appended.State.Session.Tabs);
        Assert.Equal(new PageCursor("n"), effect.Cursor);
        Assert.Equal(LoadReason.NextPage, effect.Reason);
        Assert.Equal(["A-1", "A-2", "A-3", "A-4"], tab.Rows.Select(row => row.Key));
        Assert.Null(tab.Next);
        Assert.Equal(1, tab.Selected);
    }

    [Fact]
    public void GivenLoadInFlight_WhenRequestingNextPageAgain_ThenNothingHappens()
    {
        var first = Loaded(Open(Start(), "project = A"), Page(2, new PageCursor("n")));
        var id = first.State.Session.Tabs[0].Id;
        var requested = _reducer.Reduce(first.State, new NextPageRequested(id));

        var again = _reducer.Reduce(requested.State, new NextPageRequested(id));

        Assert.Same(requested.State, again.State);
        Assert.Empty(again.Effects);
    }

    [Fact]
    public void GivenLastPageLoaded_WhenRequestingNextPage_ThenNothingHappens()
    {
        var first = Loaded(Open(Start(), "project = A"), Page(2, null));

        var again = _reducer.Reduce(first.State, new NextPageRequested(first.State.Session.Tabs[0].Id));

        Assert.Same(first.State, again.State);
    }

    [Fact]
    public void GivenSupersededRequest_WhenTheOldReplyArrives_ThenItIsIgnored()
    {
        var first = Loaded(Open(Start(), "project = A"), Page(2, new PageCursor("n")));
        var id = first.State.Session.Tabs[0].Id;
        var requested = _reducer.Reduce(first.State, new NextPageRequested(id));
        var staleId = requested.State.Session.Tabs[0].RequestId - 1;

        var stale = _reducer.Reduce(requested.State, new ViewPageLoaded(id, staleId, Page(5, null), _now));
        var failure = _reducer.Reduce(requested.State, new ViewLoadFailed(id, staleId, Error(), _now));

        Assert.Same(requested.State, stale.State);
        Assert.Same(requested.State, failure.State);
    }

    [Fact]
    public void GivenAnAppliedReply_WhenItIsDeliveredTwice_ThenTheSecondIsIgnored()
    {
        var opened = Open(Start(), "project = A");
        var applied = Loaded(opened, Page(2, null));
        var requestId = opened.State.Session.Tabs[0].RequestId;

        var again = _reducer.Reduce(
            applied.State,
            new ViewPageLoaded(opened.State.Session.Tabs[0].Id, requestId, Page(9, null), _now));

        Assert.Same(applied.State, again.State);
    }

    [Fact]
    public void GivenClosedTab_WhenItsReplyArrives_ThenItIsIgnored()
    {
        var opened = Open(Start(), "project = A");
        var tab = opened.State.Session.Tabs[0];
        var closed = _reducer.Reduce(opened.State, new TabClosed(tab.Id));

        var late = _reducer.Reduce(closed.State, new ViewPageLoaded(tab.Id, tab.RequestId, Page(2, null), _now));

        Assert.Same(closed.State, late.State);
        Assert.Empty(late.State.Session.Tabs);
    }

    [Fact]
    public void GivenTabWithRows_WhenARefreshFails_ThenRowsStayAndTheTabIsMarkedStaleAndFailed()
    {
        var loaded = Loaded(Open(Start(), "project = A"), Page(2, new PageCursor("n")));
        var id = loaded.State.Session.Tabs[0].Id;
        var requested = _reducer.Reduce(loaded.State, new NextPageRequested(id));
        var requestId = requested.State.Session.Tabs[0].RequestId;

        var failed = _reducer.Reduce(requested.State, new ViewLoadFailed(id, requestId, Error(), _now));

        var tab = Assert.Single(failed.State.Session.Tabs);
        var state = Assert.IsType<Failed>(tab.Load);
        Assert.Equal(JiraErrorKind.Timeout, state.Kind);
        Assert.Equal(LoadErrorText.For(Error()), state.Message);
        Assert.Equal(2, tab.Rows.Count);
        Assert.True(tab.IsStale);
        Assert.Equal(0, tab.RequestId);
    }

    [Fact]
    public void GivenOpeningTabWithoutRows_WhenTheLoadFails_ThenTheTabIsFailedAndNotStale()
    {
        var opened = Open(Start(), "project = A");
        var tab = opened.State.Session.Tabs[0];

        var failed = _reducer.Reduce(opened.State, new ViewLoadFailed(tab.Id, tab.RequestId, Error(), _now));

        var result = Assert.Single(failed.State.Session.Tabs);
        Assert.IsType<Failed>(result.Load);
        Assert.False(result.IsStale);
    }

    [Fact]
    public void GivenFailedNextPage_WhenRequestingAgain_ThenTheSameCursorIsRequestedWithANewId()
    {
        var loaded = Loaded(Open(Start(), "project = A"), Page(2, new PageCursor("n")));
        var id = loaded.State.Session.Tabs[0].Id;
        var requested = _reducer.Reduce(loaded.State, new NextPageRequested(id));
        var failed = _reducer.Reduce(
            requested.State,
            new ViewLoadFailed(id, requested.State.Session.Tabs[0].RequestId, Error(), _now));

        var retry = _reducer.Reduce(failed.State, new NextPageRequested(id));

        var effect = Assert.IsType<LoadViewPageEffect>(Assert.Single(retry.Effects));
        Assert.Equal(new PageCursor("n"), effect.Cursor);
        Assert.NotEqual(requested.State.Session.Tabs[0].RequestId, effect.RequestId);
    }

    [Fact]
    public void GivenTabsLoaded_WhenOpeningMore_ThenEveryIdIsUniqueAndTheNewestIsActive()
    {
        var one = Open(Start(), "project = A");
        var two = Open(one.State, "project = B");

        var tabs = two.State.Session.Tabs;
        Assert.Equal(2, tabs.Count);
        Assert.Equal(tabs[1].Id, two.State.Session.Active);
        Assert.NotEqual(tabs[0].Id, tabs[1].Id);
        Assert.NotEqual(tabs[0].RequestId, tabs[1].RequestId);
        Assert.True(two.State.Sequence > one.State.Sequence);
    }

    [Fact]
    public void GivenHiddenTab_WhenAnotherLoadsAndSelects_ThenTheHiddenTabKeepsItsState()
    {
        var one = Loaded(Open(Start(), "project = A"), Page(3, null));
        var firstId = one.State.Session.Tabs[0].Id;
        var selected = SetSelection(one.State, firstId, 2);
        var two = Open(selected, "project = B");

        var back = _reducer.Reduce(two.State, new TabActivated(firstId));

        var tab = back.State.Session.Tabs[0];
        Assert.Equal(firstId, back.State.Session.Active);
        Assert.Equal(2, tab.Selected);
        Assert.Equal("A-3", tab.SelectedKey);
        Assert.Equal(3, tab.Rows.Count);
        Assert.Empty(back.Effects);
    }

    [Fact]
    public void GivenUnknownTab_WhenActivatingOrChangingLayout_ThenNothingHappens()
    {
        var opened = Open(Start(), "project = A").State;
        var unknown = new TabId(9999);

        var activated = _reducer.Reduce(opened, new TabActivated(unknown));
        var layout = _reducer.Reduce(opened, new ViewLayoutChanged(unknown, ViewLayout.Columns));
        var closed = _reducer.Reduce(opened, new TabClosed(unknown));

        Assert.Same(opened, activated.State);
        Assert.Same(opened, layout.State);
        Assert.Same(opened, closed.State);
        Assert.Empty(closed.Effects);
    }

    [Fact]
    public void GivenThreeTabs_WhenClosingTheActiveMiddleOne_ThenTheRightNeighbourBecomesActive()
    {
        var state = OpenThree();
        var middle = state.Session.Tabs[1].Id;
        var active = _reducer.Reduce(state, new TabActivated(middle)).State;

        var closed = _reducer.Reduce(active, new TabClosed(middle));

        Assert.Equal(state.Session.Tabs[2].Id, closed.State.Session.Active);
        Assert.Equal(2, closed.State.Session.Tabs.Count);
        Assert.Equal(middle, Assert.IsType<CancelTabEffect>(Assert.Single(closed.Effects)).Tab);
    }

    [Fact]
    public void GivenThreeTabs_WhenClosingTheActiveLastOne_ThenTheLeftNeighbourBecomesActive()
    {
        var state = OpenThree();

        var closed = _reducer.Reduce(state, new TabClosed(state.Session.Tabs[2].Id));

        Assert.Equal(state.Session.Tabs[1].Id, closed.State.Session.Active);
    }

    [Fact]
    public void GivenThreeTabs_WhenClosingAHiddenOne_ThenTheActiveTabStays()
    {
        var state = OpenThree();

        var closed = _reducer.Reduce(state, new TabClosed(state.Session.Tabs[0].Id));

        Assert.Equal(state.Session.Active, closed.State.Session.Active);
        Assert.Equal(2, closed.State.Session.Tabs.Count);
    }

    [Fact]
    public void GivenOneTab_WhenClosingIt_ThenNoTabIsActiveAndItsWorkIsCancelled()
    {
        var opened = Open(Start(), "project = A");
        var id = opened.State.Session.Tabs[0].Id;

        var closed = _reducer.Reduce(opened.State, new TabClosed(id));

        Assert.Empty(closed.State.Session.Tabs);
        Assert.Null(closed.State.Session.Active);
        Assert.Equal(id, Assert.IsType<CancelTabEffect>(Assert.Single(closed.Effects)).Tab);
    }

    [Fact]
    public void GivenTab_WhenChangingLayout_ThenOnlyTheLayoutChanges()
    {
        var loaded = Loaded(Open(Start(), "project = A"), Page(2, null));
        var before = loaded.State.Session.Tabs[0];

        var changed = _reducer.Reduce(loaded.State, new ViewLayoutChanged(before.Id, ViewLayout.Columns));

        var after = changed.State.Session.Tabs[0];
        Assert.Equal(ViewLayout.Columns, after.Definition.Layout);
        Assert.Equal(before with { Definition = before.Definition with { Layout = ViewLayout.Columns } }, after);
    }

    [Fact]
    public void GivenTabs_WhenTheViewportChanges_ThenEveryTabAndNewTabsUseIt()
    {
        var state = OpenThree();

        var resized = _reducer.Reduce(state, new ViewportChanged(30)).State;
        var opened = _reducer.Reduce(resized, new ViewOpenRequested(Definition("project = D"))).State;

        Assert.All(resized.Session.Tabs, tab => Assert.Equal(30, tab.VisibleRows));
        Assert.Equal(30, opened.Session.Tabs[3].VisibleRows);
    }

    [Fact]
    public void GivenTabsWithRows_WhenTheProfileSwitches_ThenAllTabsAreDroppedAndIdsKeepGrowing()
    {
        var dev = new Profile("dev", "https://dev.atlassian.net", "me@dev.test");
        var catalog = ProfileCatalog.Empty.With(_work).With(dev).Activate("work");
        var state = Loaded(Open(Start(catalog), "project = A"), Page(2, null)).State;
        var oldTab = state.Session.Tabs[0];

        var switched = _reducer.Reduce(state, new ProfilesChanged(catalog.Activate("dev"), "Done."));
        var reopened = _reducer.Reduce(switched.State, new ViewOpenRequested(Definition("project = B")));
        var reply = _reducer.Reduce(
            reopened.State,
            new ViewPageLoaded(oldTab.Id, oldTab.RequestId, Page(5, null), _now));

        Assert.Empty(switched.State.Session.Tabs);
        Assert.Null(switched.State.Session.Active);
        Assert.Equal(state.Sequence, switched.State.Sequence);
        Assert.True(reopened.State.Session.Tabs[0].Id.Value > oldTab.Id.Value);
        Assert.Same(reopened.State, reply.State);
    }

    [Fact]
    public void GivenAPageOverTheTabCap_WhenAppending_ThenRowsAreCappedAndTheTabIsTruncated()
    {
        var first = Loaded(Open(Start(), "project = A"), Page(9_950, new PageCursor("n")));
        var id = first.State.Session.Tabs[0].Id;
        var requested = _reducer.Reduce(first.State, new NextPageRequested(id));

        var appended = Reply(requested, Page(100, new PageCursor("m"), 9_951));

        var tab = appended.State.Session.Tabs[0];
        Assert.Equal(ViewPaging.MaxRowsPerTab, tab.Rows.Count);
        Assert.True(tab.Truncated);
        Assert.Null(tab.Next);
        Assert.Same(appended.State, _reducer.Reduce(appended.State, new NextPageRequested(id)).State);
    }

    [Fact]
    public void GivenExactlyTheTabCap_WhenLoaded_ThenTheTabIsNotTruncated()
    {
        var loaded = Loaded(Open(Start(), "project = A"), Page(ViewPaging.MaxRowsPerTab, null));

        var tab = loaded.State.Session.Tabs[0];
        Assert.Equal(ViewPaging.MaxRowsPerTab, tab.Rows.Count);
        Assert.False(tab.Truncated);
    }

    [Fact]
    public void GivenExactlyTheTabCapWithMorePages_WhenLoaded_ThenTheTabIsTruncatedAndStopsPaging()
    {
        var loaded = Loaded(Open(Start(), "project = A"), Page(ViewPaging.MaxRowsPerTab, new PageCursor("n")));

        var tab = loaded.State.Session.Tabs[0];
        Assert.True(tab.Truncated);
        Assert.Null(tab.Next);
    }

    [Fact]
    public void GivenSameViewportHeight_WhenReportedAgain_ThenTheStateIsTheSameInstance()
    {
        var state = _reducer.Reduce(OpenThree(), new ViewportChanged(30)).State;

        var again = _reducer.Reduce(state, new ViewportChanged(30));

        Assert.Same(state, again.State);
    }

    [Fact]
    public void GivenTotalOverTheLimit_WhenAnotherTabLoads_ThenTheLeastRecentHiddenTabGoesCold()
    {
        var state = Start();
        state = FillTab(state, "project = A", 10_000);
        state = FillTab(state, "project = B", 10_000);
        state = FillTab(state, "project = C", 10_000);
        state = FillTab(state, "project = D", 10_000);
        state = FillTab(state, "project = E", 10_000);
        var oldest = state.Session.Tabs[0];

        var result = Open(state, "project = F");
        var loaded = Loaded(result, Page(100, null));

        var tabs = loaded.State.Session.Tabs;
        Assert.True(tabs[0].Cold);
        Assert.Empty(tabs[0].Rows);
        Assert.Equal(0, tabs[0].RequestId);
        Assert.Equal(LoadState.Idle, tabs[0].Load);
        Assert.Equal(oldest.Definition, tabs[0].Definition);
        Assert.All(tabs.Skip(1), tab => Assert.False(tab.Cold));
        Assert.Contains(loaded.Effects, effect => effect is CancelTabEffect cancel && cancel.Tab == oldest.Id);
    }

    [Fact]
    public void GivenColdTab_WhenActivated_ThenItReloadsItsFirstPageAndKeepsItsSelectionKey()
    {
        var state = Start();
        state = FillTab(state, "project = A", 10_000);
        state = FillTab(state, "project = B", 10_000);
        state = FillTab(state, "project = C", 10_000);
        state = FillTab(state, "project = D", 10_000);
        state = FillTab(state, "project = E", 10_000);
        var cooled = Loaded(Open(state, "project = F"), Page(100, null)).State;
        var cold = cooled.Session.Tabs[0];

        var activated = _reducer.Reduce(cooled, new TabActivated(cold.Id));

        var tab = activated.State.Session.Tabs[0];
        var effect = Assert.IsType<LoadViewPageEffect>(Assert.Single(activated.Effects.OfType<LoadViewPageEffect>()));
        Assert.False(tab.Cold);
        Assert.Equal(new Loading(LoadReason.Open), tab.Load);
        Assert.Equal(tab.RequestId, effect.RequestId);
        Assert.Equal(cold.SelectedKey, tab.SelectedKey);
        Assert.Equal(cold.Id, activated.State.Session.Active);
    }

    [Fact]
    public void GivenSelectedKeyThatMovedOrVanished_WhenFirstPageIsReplaced_ThenSelectionFollowsTheKeyOrIsClamped()
    {
        var cold = ColdTabWithSelection(2);

        var reloaded = Reply(_reducer.Reduce(cold, new TabActivated(cold.Session.Tabs[0].Id)), Page(2, null));

        var tab = reloaded.State.Session.Tabs[0];
        Assert.Equal(1, tab.Selected);
        Assert.Equal("A-2", tab.SelectedKey);
    }

    [Theory]
    [InlineData(JiraErrorKind.Unauthorized)]
    [InlineData(JiraErrorKind.NotFound)]
    [InlineData(JiraErrorKind.Network)]
    [InlineData(JiraErrorKind.BadRequest)]
    public void GivenAnyErrorKind_WhenBuildingTheText_ThenItIsNotEmpty(JiraErrorKind kind)
    {
        var text = LoadErrorText.For(new JiraError(kind, null, "Jira said no"));

        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    private static AppState Start(ProfileCatalog? catalog = null)
    {
        var profiles = catalog ?? ProfileCatalog.Empty.With(_work);

        return AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
    }

    private static ViewDefinition Definition(string jql)
    {
        return new ViewDefinition(ViewSourceKind.Jql, jql, ViewLayout.List, jql);
    }

    private static JiraError Error()
    {
        return new JiraError(JiraErrorKind.Timeout, null, "timed out");
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
            number.ToString(System.Globalization.CultureInfo.InvariantCulture),
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

    private ReduceResult Open(AppState state, string jql)
    {
        return _reducer.Reduce(state, new ViewOpenRequested(Definition(jql)));
    }

    /// <summary>Delivers a page for the current request of the active tab in <paramref name="previous"/>.</summary>
    private ReduceResult Reply(ReduceResult previous, IssuePage page)
    {
        var active = previous.State.Session.Active;
        var tab = previous.State.Session.Tabs.Single(candidate => candidate.Id == active);

        return _reducer.Reduce(previous.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now));
    }

    private ReduceResult Loaded(ReduceResult opened, IssuePage page)
    {
        return Reply(opened, page);
    }

    private static AppState SetSelection(AppState state, TabId id, int index)
    {
        var tabs = state.Session.Tabs
            .Select(tab => tab.Id == id ? tab with { Selected = index, SelectedKey = tab.Rows[index].Key } : tab)
            .ToArray();

        return state with { Session = state.Session with { Tabs = tabs } };
    }

    private AppState OpenThree()
    {
        var state = Start();

        foreach (var jql in new[] { "project = A", "project = B", "project = C" })
        {
            state = Open(state, jql).State;
        }

        return state;
    }

    private AppState FillTab(AppState state, string jql, int rows)
    {
        return Loaded(Open(state, jql), Page(rows, null)).State;
    }

    private AppState ColdTabWithSelection(int selected)
    {
        var state = Loaded(Open(Start(), "project = A"), Page(3, null)).State;
        var id = state.Session.Tabs[0].Id;
        var moved = SetSelection(state, id, selected);
        var tab = moved.Session.Tabs[0] with
        {
            Rows = [],
            Cold = true,
            Load = LoadState.Idle,
            RequestId = 0,
            SelectedKey = "A-2",
        };

        return moved with { Session = moved.Session with { Tabs = [tab], Active = null } };
    }
}
