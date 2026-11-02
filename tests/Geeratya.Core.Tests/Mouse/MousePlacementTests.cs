using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Mouse;

/// <summary>Clicks on the tab bar, the list, the board columns and the opened issue, through the real reducer.</summary>
[Trait("Os", "Independent")]
public class MousePlacementTests
{
    private const int HeaderRows = 2;
    private const int ListHeader = 2;

    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueTypeSummary _task = new("10", "Task", 0, false);

    private static readonly BoardConfiguration _configuration = new(
        4,
        "Board",
        BoardType.Kanban,
        [new BoardColumn("To Do", ["1"]), new BoardColumn("Doing", ["2"]), new BoardColumn("Done", ["4"])],
        "10001",
        null,
        null,
        null);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Theory]
    [InlineData(80, 24)]
    [InlineData(40, 12)]
    [InlineData(120, 40)]
    public void GivenList_WhenClickingARow_ThenItIsSelectedAndTheTitleRowsDoNothing(int width, int height)
    {
        var state = Loaded(ViewLayout.List, width, height, "1:A-1", "1:A-2", "1:A-3", "1:A-4");

        var third = Click(state, width, height, 2, HeaderRows + ListHeader + 2);
        var onTitle = Click(third, width, height, 2, HeaderRows);
        var onColumnTitles = Click(third, width, height, 2, HeaderRows + 1);

        Assert.Equal("A-3", Active(third).SelectedKey);
        Assert.Equal("A-3", Active(onTitle).SelectedKey);
        Assert.Equal("A-3", Active(onColumnTitles).SelectedKey);
    }

    [Fact]
    public void GivenList_WhenClickingBelowTheLastRow_ThenTheSelectionStays()
    {
        var state = Loaded(ViewLayout.List, 80, 24, "1:A-1", "1:A-2");

        var result = Click(state, 80, 24, 2, HeaderRows + ListHeader + 10);

        Assert.Equal("A-1", Active(result).SelectedKey);
    }

    [Fact]
    public void GivenScrolledList_WhenClickingTheFirstShownRow_ThenTheRowOfTheScrollIsSelected()
    {
        var keys = Enumerable.Range(1, 40).Select(number => $"1:A-{number}").ToArray();
        var state = Loaded(ViewLayout.List, 80, 24, keys);
        var scrolled = _reducer.Reduce(state, new ActionRequested(ActionIds.NavigateBottom)).State;
        var scroll = Active(scrolled).Scroll;

        var result = Click(scrolled, 80, 24, 2, HeaderRows + ListHeader);

        Assert.True(scroll > 0);
        Assert.Equal($"A-{scroll + 1}", Active(result).SelectedKey);
    }

    [Fact]
    public void GivenList_WhenDoubleClickingARow_ThenItIsSelectedAndOpened()
    {
        var state = Loaded(ViewLayout.List, 80, 24, "1:A-1", "1:A-2", "1:A-3");

        var (result, effects) = Resolve(state, 80, 24, 2, HeaderRows + ListHeader + 1, MouseGesture.DoubleClick);

        Assert.Equal("A-2", Assert.Single(Active(result).Detail).Key);
        Assert.Single(effects.OfType<LoadIssueDetailEffect>());
    }

    [Fact]
    public void GivenList_WhenRightClickingARow_ThenItIsSelectedAndThePaletteOpens()
    {
        var state = Loaded(ViewLayout.List, 80, 24, "1:A-1", "1:A-2", "1:A-3");

        var (result, _) = Resolve(state, 80, 24, 2, HeaderRows + ListHeader + 2, MouseGesture.RightClick);

        Assert.Equal("A-3", Active(result).SelectedKey);
        Assert.Equal(InputMode.Command, result.Mode);
    }

    [Fact]
    public void GivenBoardColumns_WhenClickingACard_ThenItsColumnAndCardAreSelected()
    {
        var state = Loaded(ViewLayout.Columns, 90, 30, "1:A-1", "2:A-2", "2:A-3", "4:A-4");

        // Three 30-wide columns: the second starts at cell 30. Full cards are five rows, the first one starts on
        // body row 2.
        var second = Click(state, 90, 30, 35, HeaderRows + 2 + 5);

        Assert.Equal("A-3", Active(second).SelectedKey);
        Assert.Equal(1, Active(second).Cursor!.Column);
    }

    [Fact]
    public void GivenBoardColumns_WhenClickingAColumnHeader_ThenTheColumnIsSelectedKeepingItsCard()
    {
        var state = Loaded(ViewLayout.Columns, 90, 30, "1:A-1", "2:A-2", "4:A-3");

        var result = Click(state, 90, 30, 80, HeaderRows + 1);

        Assert.Equal("A-3", Active(result).SelectedKey);
        Assert.Equal(2, Active(result).Cursor!.Column);
    }

    [Fact]
    public void GivenBoardColumns_WhenClickingBelowTheCards_ThenNothingChanges()
    {
        var state = Loaded(ViewLayout.Columns, 90, 30, "1:A-1", "2:A-2");

        var result = Click(state, 90, 30, 35, HeaderRows + 2 + 15);

        Assert.Equal(Active(state).SelectedKey, Active(result).SelectedKey);
        Assert.Equal(Active(state).Cursor?.Column, Active(result).Cursor?.Column);
    }

    [Fact]
    public void GivenNarrowBoard_WhenClickingACard_ThenTheSingleDrawnColumnIsUsed()
    {
        var state = Loaded(ViewLayout.Columns, 40, 30, "1:A-1", "1:A-2");

        var result = Click(state, 40, 30, 5, HeaderRows + 2 + 5);

        Assert.Equal("A-2", Active(result).SelectedKey);
    }

    [Fact]
    public void GivenCompactBoard_WhenClickingACard_ThenCardsAreOneRowEach()
    {
        var state = Loaded(ViewLayout.Columns, 90, 10, "1:A-1", "1:A-2", "1:A-3");

        var result = Click(state, 90, 10, 5, HeaderRows + 2 + 2);

        Assert.Equal("A-3", Active(result).SelectedKey);
    }

    [Theory]
    [InlineData(120)]
    [InlineData(80)]
    [InlineData(40)]
    public void GivenTabs_WhenClickingATitle_ThenThatTabBecomesActive(int width)
    {
        var state = TwoTabs(width);
        var areas = TabBar.Areas(state.Session.Tabs, state.Session.Active, width);
        var first = areas[0];

        var (result, _) = Resolve(state, width, 24, first.Start, 0, MouseGesture.Click);

        Assert.Equal(state.Session.Tabs[0].Id, result.Session.Active);
        Assert.Equal(2, areas.Count);
        Assert.True(areas[0].End <= areas[1].Start);
    }

    [Fact]
    public void GivenTabs_WhenClickingTheSeparatorOrRightOfTheLastTab_ThenNothingHappens()
    {
        var state = TwoTabs(80);
        var areas = TabBar.Areas(state.Session.Tabs, state.Session.Active, 80);

        Assert.Equal(state, Resolve(state, 80, 24, areas[0].End, 0, MouseGesture.Click).State);
        Assert.Equal(state, Resolve(state, 80, 24, areas[1].End + 3, 0, MouseGesture.Click).State);
    }

    [Fact]
    public void GivenNoBanner_WhenClickingTheBannerRow_ThenNothingHappens()
    {
        var state = Loaded(ViewLayout.List, 80, 24, "1:A-1");
        var context = MouseContext.From(state, 80, 24, HeaderRows, _now);

        Assert.False(context.BannerShown);
        Assert.Empty(MouseResolver.Resolve(context, 5, 1, MouseGesture.Click, _now));
    }

    [Fact]
    public void GivenBanner_WhenClickingTheBannerRow_ThenTheViewRefreshes()
    {
        var context = new MouseContext(80, 24, HeaderRows, false, false) { BannerShown = true };

        var commands = MouseResolver.Resolve(context, 5, 1, MouseGesture.Click, _now);

        Assert.Equal(ActionIds.ViewRefresh, Assert.IsType<ActionRequested>(Assert.Single(commands)).Action);
    }

    [Fact]
    public void GivenOpenedIssue_WhenClickingALine_ThenTheCursorMovesThereWithoutScrolling()
    {
        var state = OpenedIssue();
        var top = Active(state).Detail[^1];
        var stops = top.Document!.Stops;
        var target = stops[3];

        var result = Click(state, 80, 24, 4, HeaderRows + DetailLayout.HeaderRows + target - top.Scroll);

        Assert.Equal(target, Active(result).Detail[^1].Cursor);
        Assert.Equal(top.Scroll, Active(result).Detail[^1].Scroll);
    }

    [Fact]
    public void GivenOpenedIssue_WhenClickingTheBreadcrumb_ThenTheCursorStays()
    {
        var state = OpenedIssue();

        var result = Click(state, 80, 24, 4, HeaderRows);

        Assert.Equal(Active(state).Detail[^1].Cursor, Active(result).Detail[^1].Cursor);
    }

    [Fact]
    public void GivenOverlayOpen_WhenClickingATab_ThenItClosesTheOverlayInsteadOfSwitching()
    {
        var state = _reducer.Reduce(TwoTabs(80), new ActionRequested(ActionIds.PaletteOpen)).State;
        var areas = TabBar.Areas(state.Session.Tabs, state.Session.Active, 80);

        var result = Resolve(state, 80, 24, areas[0].Start, 0, MouseGesture.Click).State;

        Assert.Equal(InputMode.Normal, result.Mode);
        Assert.Equal(state.Session.Active, result.Session.Active);
    }

    private static TabState Active(AppState state)
    {
        return state.Session.Tabs.Single(candidate => candidate.Id == state.Session.Active);
    }

    private static AppState Start()
    {
        var profiles = ProfileCatalog.Empty.With(_work);

        return AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(_work) };
    }

    private static IssueSummary Issue(string spec)
    {
        var parts = spec.Split(':');

        return new IssueSummary(
            parts[1],
            parts[1],
            $"Summary {parts[1]}",
            new IssueStatus(parts[0], "Status", "new"),
            _task,
            null,
            null,
            [],
            null,
            null,
            "A");
    }

    private AppState Click(AppState state, int width, int height, int column, int row)
    {
        return Resolve(state, width, height, column, row, MouseGesture.Click).State;
    }

    private ReduceResult Resolve(AppState state, int width, int height, int column, int row, MouseGesture gesture)
    {
        var context = MouseContext.From(state, width, height, HeaderRows, _now);
        var current = new ReduceResult(state, []);

        foreach (var command in MouseResolver.Resolve(context, column, row, gesture, _now))
        {
            var next = _reducer.Reduce(current.State, command);

            current = new ReduceResult(next.State, [.. current.Effects, .. next.Effects]);
        }

        return current;
    }

    private AppState Loaded(ViewLayout layout, int width, int height, params string[] specs)
    {
        var rows = specs.Select(Issue).ToArray();
        var source = layout == ViewLayout.Columns ? ViewSourceKind.Board : ViewSourceKind.Jql;
        var definition = new ViewDefinition(source, source == ViewSourceKind.Board ? "4" : "project = A", layout, "View");
        var sized = _reducer.Reduce(Start(), new ViewportChanged(height - HeaderRows - 1 - ListHeader, width)).State;
        var opened = _reducer.Reduce(sized, new ViewOpenRequested(definition)).State;
        var tab = Active(opened);
        var configured = layout == ViewLayout.Columns
            ? _reducer.Reduce(opened, new BoardConfigurationLoaded(tab.Id, tab.RequestId, _configuration)).State
            : opened;

        return _reducer.Reduce(configured, new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage(rows, null, rows.Length), _now))
            .State;
    }

    private AppState TwoTabs(int width)
    {
        var state = Loaded(ViewLayout.List, width, 24, "1:A-1");
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = B", ViewLayout.List, "project = B");

        return _reducer.Reduce(state, new ViewOpenRequested(definition)).State;
    }

    private AppState OpenedIssue()
    {
        var state = Loaded(ViewLayout.List, 80, 24, "1:A-1", "1:A-2");
        var opened = _reducer.Reduce(state, new ActionRequested(ActionIds.ItemOpen)).State;
        var tab = Active(opened);
        var issue = new IssueDetail(
            "1",
            "A-1",
            "Issue 1",
            _todo,
            _task,
            "High",
            null,
            null,
            [],
            _now.AddDays(-3),
            _now.AddHours(-1),
            "A",
            null,
            null,
            [],
            [],
            []);

        return _reducer.Reduce(opened, new IssueDetailLoaded(tab.Id, tab.Detail[0].IssueRequest, issue, _now)).State;
    }
}
