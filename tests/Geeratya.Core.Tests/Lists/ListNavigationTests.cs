using System.Globalization;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Tests.Lists;

/// <summary>The nav keys through the real reducer: key press, selection, scroll and next-page trigger.</summary>
[Trait("Os", "Independent")]
public class ListNavigationTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenALoadedList_WhenPressingJAndK_ThenTheSelectionMovesByOneRow()
    {
        var state = Loaded(50, 20, "n2");

        state = Press(state, "j").State;
        state = Press(state, "Down").State;

        Assert.Equal(2, Tab(state).Selected);
        Assert.Equal("A-3", Tab(state).SelectedKey);

        state = Press(state, "k").State;
        state = Press(state, "Up").State;
        state = Press(state, "k").State;

        Assert.Equal(0, Tab(state).Selected);
    }

    [Fact]
    public void GivenALoadedList_WhenJumpingWithGgAndG_ThenSelectionAndScrollFollow()
    {
        var state = Loaded(50, 10, null);

        state = Press(state, "G").State;

        Assert.Equal(49, Tab(state).Selected);
        Assert.Equal(40, Tab(state).Scroll);

        state = Press(Press(state, "g").State, "g").State;

        Assert.Equal(0, Tab(state).Selected);
        Assert.Equal(0, Tab(state).Scroll);
    }

    [Theory]
    [InlineData("Ctrl-d", "Ctrl-u")]
    [InlineData("PageDown", "PageUp")]
    public void GivenALoadedList_WhenPaging_ThenItMovesHalfTheViewport(string down, string up)
    {
        var state = Loaded(100, 20, null);

        state = Press(state, down).State;

        Assert.Equal(10, Tab(state).Selected);

        state = Press(state, up).State;

        Assert.Equal(0, Tab(state).Selected);
    }

    [Fact]
    public void GivenAListWithMoreRows_WhenMovingNearTheEnd_ThenTheNextPageIsRequested()
    {
        var state = Loaded(100, 10, "n2");

        var result = Press(state, "G");

        var effect = Assert.IsType<LoadViewPageEffect>(Assert.Single(result.Effects));
        Assert.Equal(LoadReason.NextPage, effect.Reason);
        Assert.Equal(new Loading(LoadReason.NextPage), Tab(result.State).Load);
    }

    [Fact]
    public void GivenAFailedNextPage_WhenMoving_ThenNoRequestIsSentUntilRetried()
    {
        var state = Loaded(100, 10, "n2");
        var requested = Press(state, "G").State;
        var error = new JiraError(JiraErrorKind.ServerError, 500, "boom");
        var failed = _reducer.Reduce(
            requested,
            new ViewLoadFailed(Tab(requested).Id, Tab(requested).RequestId, error, _now)).State;

        var result = Press(failed, "k");

        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenAPageAppend_WhenItArrives_ThenTheSelectionStaysOnItsIssue()
    {
        var state = Loaded(100, 10, "n2");
        state = Press(state, "G").State;
        var before = Tab(state);

        state = _reducer.Reduce(state, new ViewPageLoaded(before.Id, before.RequestId, Page(100, 101), _now)).State;

        Assert.Equal(99, Tab(state).Selected);
        Assert.Equal("A-100", Tab(state).SelectedKey);
        Assert.Equal(200, Tab(state).Rows.Count);
    }

    [Fact]
    public void GivenARefreshThatMovesRows_WhenItArrives_ThenSelectionFollowsTheKeyAndStaysVisible()
    {
        var state = Loaded(100, 10, null);
        state = Press(state, "G").State;
        var loaded = Tab(state);
        var opened = state with
        {
            Session = state.Session with { Tabs = [loaded with { Load = new Loading(LoadReason.Retry), RequestId = 77 }] },
        };

        var refreshed = _reducer.Reduce(opened, new ViewPageLoaded(loaded.Id, 77, Page(100, 11), _now)).State;

        var tab = Tab(refreshed);
        Assert.Equal("A-100", tab.SelectedKey);
        Assert.Equal(89, tab.Selected);
        Assert.InRange(tab.Selected, tab.Scroll, tab.Scroll + 9);
    }

    [Fact]
    public void GivenTheViewportGrows_WhenReported_ThenTheScrollIsAdjusted()
    {
        var state = Loaded(30, 5, null);
        state = Press(state, "G").State;

        state = _reducer.Reduce(state, new ViewportChanged(40)).State;

        Assert.Equal(0, Tab(state).Scroll);
        Assert.Equal(29, Tab(state).Selected);
    }

    [Fact]
    public void GivenSelectionOutsideReach_WhenTheViewportGrowsPastIt_ThenTheNextPageIsRequested()
    {
        var state = Loaded(100, 10, "n2");
        state = _reducer.Reduce(state, new SelectionMoved(Tab(state).Id, 85)).State;

        var result = _reducer.Reduce(state, new ViewportChanged(20));

        var effect = Assert.IsType<LoadViewPageEffect>(Assert.Single(result.Effects));
        Assert.Equal(LoadReason.NextPage, effect.Reason);
    }

    [Fact]
    public void GivenNoTabsOrAnEmptyList_WhenPressingNavKeys_ThenNothingChanges()
    {
        var empty = Loaded(0, 10, null);

        foreach (var key in new[] { "j", "k", "G", "Ctrl-d", "PageUp" })
        {
            Assert.Equal(AppState.Initial, Press(AppState.Initial, key).State);
            Assert.Equal(empty, Press(empty, key).State);
        }
    }

    [Fact]
    public void GivenHelpIsOpen_WhenPressingJ_ThenTheHelpScrollsAndTheListStays()
    {
        var state = Loaded(20, 10, null) with { Mode = InputMode.Help };

        var result = Press(state, "J").State;

        Assert.Equal(1, result.HelpScroll);
        Assert.Equal(0, Tab(result).Selected);
    }

    [Fact]
    public void GivenNavActions_WhenListingUnimplemented_ThenNoneOfTheListMotionsRemain()
    {
        string[] implemented =
        [
            ActionIds.NavigateDown,
            ActionIds.NavigateUp,
            ActionIds.NavigateTop,
            ActionIds.NavigateBottom,
            ActionIds.NavigatePageDown,
            ActionIds.NavigatePageUp,
        ];

        Assert.Empty(ActionCatalog.Unimplemented.Intersect(implemented));
    }

    private static TabState Tab(AppState state)
    {
        return state.Session.Tabs[0];
    }

    private static AppState Start()
    {
        var profiles = ProfileCatalog.Empty.With(_work);

        return AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
    }

    private static IssuePage Page(int count, int first, string? next = null)
    {
        var items = Enumerable.Range(first, count).Select(Issue).ToArray();

        return new IssuePage(items, next is null ? null : new PageCursor(next), null);
    }

    private static IssueSummary Issue(int number)
    {
        return new IssueSummary(
            number.ToString(CultureInfo.InvariantCulture),
            $"A-{number}",
            "Summary",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
    }

    private ReduceResult Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key));
    }

    private AppState Loaded(int rows, int viewport, string? next)
    {
        var opened = _reducer.Reduce(Start(), new ViewOpenRequested(
            new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A")));
        var tab = Tab(opened.State);
        var sized = _reducer.Reduce(opened.State, new ViewportChanged(viewport));

        return _reducer.Reduce(sized.State, new ViewPageLoaded(tab.Id, tab.RequestId, Page(rows, 1, next), _now)).State;
    }
}
