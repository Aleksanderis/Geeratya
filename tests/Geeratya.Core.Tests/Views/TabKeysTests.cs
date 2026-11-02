using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Views;

[Trait("Os", "Independent")]
public class TabKeysTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenThreeTabs_WhenPressingTabAndShiftTab_ThenTheActiveTabWrapsAround()
    {
        var state = OpenTabs(3);
        var ids = state.Session.Tabs.Select(tab => tab.Id).ToArray();

        var next = Press(state, "Tab");
        var wrapped = Press(Press(next.State, "Tab").State, "Tab");
        var previous = Press(wrapped.State, "Shift-Tab");

        Assert.Equal(ids[0], next.State.Session.Active);
        Assert.Equal(ids[2], wrapped.State.Session.Active);
        Assert.Equal(ids[1], previous.State.Session.Active);
    }

    [Fact]
    public void GivenThreeTabs_WhenPressingGTAndGShiftT_ThenTheyStepLikeTabAndShiftTab()
    {
        var state = OpenTabs(3);
        var ids = state.Session.Tabs.Select(tab => tab.Id).ToArray();

        var next = Press(Press(state, "g").State, "t");
        var previous = Press(Press(next.State, "g").State, "T");

        Assert.Equal(ids[0], next.State.Session.Active);
        Assert.Equal(ids[2], previous.State.Session.Active);
    }

    [Fact]
    public void GivenOneTab_WhenStepping_ThenNothingChanges()
    {
        var state = OpenTabs(1);

        var result = Press(state, "Tab");

        Assert.Equal(state.Session.Active, result.State.Session.Active);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenThreeTabs_WhenPressingAltNumbers_ThenThatTabBecomesActive()
    {
        var state = OpenTabs(3);
        var ids = state.Session.Tabs.Select(tab => tab.Id).ToArray();

        var second = Press(state, "Alt-2");
        var missing = Press(second.State, "Alt-7");

        Assert.Equal(ids[1], second.State.Session.Active);
        Assert.Equal(ids[1], missing.State.Session.Active);
        Assert.Equal("No tab 7.", missing.State.Message);
    }

    [Fact]
    public void GivenTwoTabsWithOwnSelectionAndScroll_WhenSwitching_ThenEachKeepsItsOwn()
    {
        var state = Open(Start(), "project = A").State;
        state = Fill(state, 30);
        state = Resize(state, 5);
        state = Move(state, 12);
        var first = state.Session.Tabs[0];
        state = Open(state, "project = B").State;
        state = Fill(state, 30);
        state = Move(state, 3);
        var second = state.Session.Tabs[1];

        var back = Press(state, "Alt-1").State;
        var forward = Press(back, "Alt-2").State;

        Assert.Equal(first.Selected, back.Session.Tabs[0].Selected);
        Assert.Equal(first.Scroll, back.Session.Tabs[0].Scroll);
        Assert.Equal(12, back.Session.Tabs[0].Selected);
        Assert.Equal(3, forward.Session.Tabs[1].Selected);
        Assert.Equal(second.Scroll, forward.Session.Tabs[1].Scroll);
        Assert.NotEqual(back.Session.Tabs[0].Scroll, forward.Session.Tabs[1].Scroll);
    }

    [Fact]
    public void GivenAnActiveTab_WhenPressingSpaceT_ThenANewTabWithTheSameViewOpensAndLoads()
    {
        var state = OpenTabs(1);

        var result = Press(Press(state, "Space").State, "t");

        Assert.Equal(2, result.State.Session.Tabs.Count);
        Assert.Equal(result.State.Session.Tabs[1].Id, result.State.Session.Active);
        Assert.Equal(state.Session.Tabs[0].Definition, result.State.Session.Tabs[1].Definition);
        Assert.IsType<LoadViewPageEffect>(Assert.Single(result.Effects));
    }

    [Fact]
    public void GivenNoTab_WhenPressingSpaceT_ThenTheStatusLineExplainsAndNothingOpens()
    {
        var result = Press(Press(Start(), "Space").State, "t");

        Assert.Empty(result.State.Session.Tabs);
        Assert.Equal("No view to copy: open one with :jql <query>.", result.State.Message);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenALoadingTab_WhenClosingIt_ThenItsLoadIsCancelledAndLateRepliesAreIgnored()
    {
        var opened = Open(Start(), "project = A");
        var tab = opened.State.Session.Tabs[0];

        var closed = Press(Press(opened.State, "Space").State, "c");
        var late = _reducer.Reduce(closed.State, new ViewPageLoaded(tab.Id, tab.RequestId, Page(2), _now));

        Assert.Equal(tab.Id, Assert.IsType<CancelTabEffect>(Assert.Single(closed.Effects)).Tab);
        Assert.Null(closed.State.Session.Active);
        Assert.Same(closed.State, late.State);
    }

    [Fact]
    public void GivenNoTab_WhenClosing_ThenTheStatusLineSaysSo()
    {
        var result = Press(Press(Start(), "Space").State, "c");

        Assert.Equal("No tab to close.", result.State.Message);
    }

    [Fact]
    public void GivenThreeTabs_WhenRunningTabCommands_ThenTheyDoWhatTheKeysDo()
    {
        var state = OpenTabs(3);
        var ids = state.Session.Tabs.Select(tab => tab.Id).ToArray();

        var first = Run(state, "tab 1");
        var next = Run(first.State, "tab next");
        var previous = Run(next.State, "tab prev");
        var closed = Run(previous.State, "tab close");
        var created = Run(closed.State, "tab new");

        Assert.Equal(ids[0], first.State.Session.Active);
        Assert.Equal(ids[1], next.State.Session.Active);
        Assert.Equal(ids[0], previous.State.Session.Active);
        Assert.Equal(2, closed.State.Session.Tabs.Count);
        Assert.Equal(ids[1], closed.State.Session.Active);
        Assert.Equal(3, created.State.Session.Tabs.Count);
    }

    [Theory]
    [InlineData("tab")]
    [InlineData("tab sideways")]
    [InlineData("tab -1")]
    public void GivenABadTabCommand_WhenRunning_ThenTheUsageIsShown(string command)
    {
        var result = Run(OpenTabs(2), command);

        Assert.Equal("Usage: :tab new|close|next|prev|<n>", result.State.Message);
    }

    [Fact]
    public void GivenTabNumberBeyondTheOpenTabs_WhenRunningTabCommand_ThenTheStatusLineNamesIt()
    {
        var result = Run(OpenTabs(2), "tab 9");

        Assert.Equal("No tab 9.", result.State.Message);
    }

    [Fact]
    public void GivenTheTabActions_WhenListingUnimplemented_ThenNoTabActionRemains()
    {
        Assert.DoesNotContain(ActionIds.TabNext, ActionCatalog.Unimplemented);
        Assert.DoesNotContain(ActionIds.TabPrevious, ActionCatalog.Unimplemented);
        Assert.DoesNotContain(ActionIds.TabGoTo9, ActionCatalog.Unimplemented);
        Assert.DoesNotContain(ActionIds.TabNew, ActionCatalog.Unimplemented);
        Assert.DoesNotContain(ActionIds.TabClose, ActionCatalog.Unimplemented);
    }

    private static AppState Start()
    {
        var profiles = ProfileCatalog.Empty.With(_work);

        return AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
    }

    private static IssuePage Page(int count)
    {
        var items = Enumerable.Range(1, count).Select(Issue).ToArray();

        return new IssuePage(items, null, null);
    }

    private static IssueSummary Issue(int number)
    {
        return new IssueSummary(
            number.ToString(System.Globalization.CultureInfo.InvariantCulture),
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

    private ReduceResult Open(AppState state, string jql)
    {
        var definition = new ViewDefinition(ViewSourceKind.Jql, jql, ViewLayout.List, jql);

        return _reducer.Reduce(state, new ViewOpenRequested(definition));
    }

    private AppState Fill(AppState state, int rows)
    {
        var tab = state.Session.Tabs.Single(candidate => candidate.Id == state.Session.Active);

        return _reducer.Reduce(state, new ViewPageLoaded(tab.Id, tab.RequestId, Page(rows), _now)).State;
    }

    private AppState Resize(AppState state, int rows)
    {
        return _reducer.Reduce(state, new ViewportChanged(rows)).State;
    }

    private AppState Move(AppState state, int index)
    {
        return _reducer.Reduce(state, new SelectionMoved(state.Session.Active!.Value, index)).State;
    }

    private AppState OpenTabs(int count)
    {
        var state = Start();

        for (var index = 1; index <= count; index++)
        {
            state = Open(state, $"project = P{index}").State;
        }

        return state;
    }

    private ReduceResult Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key));
    }

    private ReduceResult Run(AppState state, string command)
    {
        var result = Press(state, ":");

        foreach (var character in command)
        {
            result = Press(result.State, character == ' ' ? "Space" : character.ToString());
        }

        return Press(result.State, "Enter");
    }
}
