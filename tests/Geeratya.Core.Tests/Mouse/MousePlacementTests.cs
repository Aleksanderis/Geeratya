using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Text;
using Geeratya.Core.Writes;
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
    public void GivenList_WhenRightClickingARow_ThenItIsSelectedAndItsMenuOpens()
    {
        var state = Loaded(ViewLayout.List, 80, 24, "1:A-1", "1:A-2", "1:A-3");

        var (result, _) = Resolve(state, 80, 24, 2, HeaderRows + ListHeader + 2, MouseGesture.RightClick);

        var actions = result.ContextMenu!.Items.Select(item => item.Action).ToArray();

        Assert.Equal("A-3", Active(result).SelectedKey);
        Assert.Equal(InputMode.Insert, result.Mode);
        Assert.Contains(ActionIds.IssueEdit, actions);
        Assert.Contains(ActionIds.RankUp, actions);
        Assert.DoesNotContain(ActionIds.TabClose, actions);
        Assert.DoesNotContain(ActionIds.Quit, actions);
        Assert.DoesNotContain(ActionIds.CommentDelete, actions);
        Assert.DoesNotContain(ActionIds.IssueUnlink, actions);
        Assert.DoesNotContain(ActionIds.IssueOpenParent, actions);
    }

    [Fact]
    public void GivenNoIssue_WhenOpeningTheMenuByKey_ThenNoMenuOpensAndTheNoticeSaysSo()
    {
        var state = Start();

        var keyed = _reducer.Reduce(_reducer.Reduce(state, new KeyPressed("Space")).State, new KeyPressed("a")).State;

        Assert.Null(keyed.ContextMenu);
        Assert.Equal(InputMode.Normal, keyed.Mode);
    }

    [Fact]
    public void GivenOpenedIssueOnAPlainLine_WhenRightClicking_ThenTheMenuHasNeitherRankNorCommentActions()
    {
        var state = OpenedIssue();

        var (result, _) = Resolve(state, 80, 24, 2, HeaderRows + 3, MouseGesture.RightClick);

        var actions = result.ContextMenu!.Items.Select(item => item.Action).ToArray();

        Assert.Contains(ActionIds.IssueTransitionPrevious, actions);
        Assert.DoesNotContain(ActionIds.RankUp, actions);
        Assert.DoesNotContain(ActionIds.CommentDelete, actions);
    }

    [Fact]
    public void GivenContextMenu_WhenListingItems_ThenEachHasAContextMenuRowInTheMouseTable()
    {
        var state = Loaded(ViewLayout.List, 80, 24, "1:A-1");
        var rowMenu = Resolve(state, 80, 24, 2, HeaderRows + ListHeader, MouseGesture.RightClick).State.ContextMenu!;
        var lineMenu = Resolve(OpenedIssue(), 80, 24, 2, HeaderRows + 3, MouseGesture.RightClick).State.ContextMenu!;

        foreach (var item in rowMenu.Items.Concat(lineMenu.Items))
        {
            Assert.Contains(
                DefaultMouseMap.Rows,
                row => row.Target == MouseTarget.ContextMenu && row.Action == item.Action);
        }
    }

    [Fact]
    public void GivenOpenMenu_WhenClickingTheDeleteRow_ThenTheDeleteFlowStartsAndNothingIsSent()
    {
        var opened = Resolve(
            Loaded(ViewLayout.List, 80, 24, "1:A-1", "1:A-2"),
            80,
            24,
            2,
            HeaderRows + ListHeader + 1,
            MouseGesture.RightClick).State;
        var index = opened.ContextMenu!.Items.ToList().FindIndex(item => item.Action == ActionIds.IssueDelete);
        var regions = new[] { new OverlayRegion(MouseTarget.ContextMenu, 6, 10, 30, 1, index) };
        var context = MouseContext.From(opened, 80, 24, HeaderRows, _now, regions);

        var commands = MouseResolver.Resolve(context, 12, 6, MouseGesture.Click, _now);
        var result = _reducer.Reduce(opened, Assert.Single(commands));

        Assert.Null(result.State.ContextMenu);
        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Contains("Checking A-2", result.State.Message);
        Assert.Single(result.Effects.OfType<CountSubtasksEffect>());
        Assert.DoesNotContain(result.Effects, effect => effect is ExecuteWriteEffect);
    }

    [Fact]
    public void GivenOpenMenu_WhenMovingAndPressingEnter_ThenTheHighlightedActionRunsLikeItsKey()
    {
        var opened = Resolve(
            Loaded(ViewLayout.List, 80, 24, "1:A-1"),
            80,
            24,
            2,
            HeaderRows + ListHeader,
            MouseGesture.RightClick).State;
        var index = opened.ContextMenu!.Items.ToList().FindIndex(item => item.Action == ActionIds.IssueDelete);
        var moved = opened;

        for (var step = 0; step < index; step++)
        {
            moved = _reducer.Reduce(moved, new KeyPressed("j")).State;
        }

        var result = _reducer.Reduce(moved, new KeyPressed("Enter")).State;

        Assert.Equal(index, moved.ContextMenu!.Selected);
        Assert.Null(result.ContextMenu);
        Assert.Contains("Checking A-1", result.Message);
    }

    [Fact]
    public void GivenOpenMenu_WhenPressingEscape_ThenItClosesWithoutActing()
    {
        var opened = Resolve(
            Loaded(ViewLayout.List, 80, 24, "1:A-1"),
            80,
            24,
            2,
            HeaderRows + ListHeader,
            MouseGesture.RightClick).State;

        var result = _reducer.Reduce(opened, new KeyPressed("Esc")).State;

        Assert.Null(result.ContextMenu);
        Assert.Equal(InputMode.Normal, result.Mode);
    }

    [Fact]
    public void GivenSpaceA_WhenPressedOnARow_ThenTheSameMenuOpensAsForARightClick()
    {
        var state = Loaded(ViewLayout.List, 80, 24, "1:A-1");

        var keyed = _reducer.Reduce(_reducer.Reduce(state, new KeyPressed("Space")).State, new KeyPressed("a")).State;

        Assert.NotNull(keyed.ContextMenu);
        Assert.Equal(InputMode.Insert, keyed.Mode);
    }

    [Fact]
    public void GivenBoardCard_WhenDraggingItToTheNextColumn_ThenTheMoveFormOpensWithoutWritingOnRelease()
    {
        var state = Loaded(ViewLayout.Columns, 90, 30, "1:A-1", "2:A-2");

        var (result, effects) = Drag(state, 90, 30, 5, HeaderRows + 2, 45, HeaderRows + 3);

        Assert.Equal("A-1", Active(result).SelectedKey);
        Assert.True(FormReducer.IsOpen(result));
        Assert.DoesNotContain(effects, effect => effect is ExecuteWriteEffect);
    }

    [Fact]
    public void GivenBoardCard_WhenDraggingItBackToThePreviousColumn_ThenItMovesLikeAltH()
    {
        var state = Loaded(ViewLayout.Columns, 90, 30, "1:A-1", "2:A-2");

        var (result, _) = Drag(state, 90, 30, 45, HeaderRows + 2, 5, HeaderRows + 2);

        Assert.Equal("A-2", Active(result).SelectedKey);
        Assert.True(FormReducer.IsOpen(result));
    }

    [Fact]
    public void GivenBoardCard_WhenDroppingTwoColumnsAway_ThenNothingMovesAndTheNoticeSaysWhy()
    {
        var state = Loaded(ViewLayout.Columns, 90, 30, "1:A-1");

        var (result, effects) = Drag(state, 90, 30, 5, HeaderRows + 2, 80, HeaderRows + 2);

        Assert.False(FormReducer.IsOpen(result));
        Assert.Empty(effects);
        Assert.Contains("next or previous", result.Message);
    }

    [Fact]
    public void GivenBoardCard_WhenDroppingInItsOwnColumnOrStartingOffACard_ThenNothingHappens()
    {
        var state = Loaded(ViewLayout.Columns, 90, 30, "1:A-1", "1:A-2");

        var same = Drag(state, 90, 30, 5, HeaderRows + 2, 6, HeaderRows + 7);
        var fromHeader = Drag(state, 90, 30, 5, HeaderRows + 1, 45, HeaderRows + 2);
        var fromBlank = Drag(state, 90, 30, 5, HeaderRows + 20, 45, HeaderRows + 2);

        foreach (var result in new[] { same, fromHeader, fromBlank })
        {
            Assert.False(FormReducer.IsOpen(result.State));
            Assert.Empty(result.Effects);
            Assert.Null(result.State.Message);
        }
    }

    [Fact]
    public void GivenListLayoutOrOverlay_WhenDragging_ThenNothingHappens()
    {
        var list = Loaded(ViewLayout.List, 90, 30, "1:A-1");
        var board = Loaded(ViewLayout.Columns, 90, 30, "1:A-1");
        var overlay = _reducer.Reduce(board, new ActionRequested(ActionIds.PaletteOpen)).State;

        var onList = Drag(list, 90, 30, 5, HeaderRows + 2, 45, HeaderRows + 2);
        var onOverlay = Drag(overlay, 90, 30, 5, HeaderRows + 2, 45, HeaderRows + 2);

        Assert.False(FormReducer.IsOpen(onList.State));
        Assert.False(FormReducer.IsOpen(onOverlay.State));
        Assert.Empty(onList.Effects);
        Assert.Empty(onOverlay.Effects);
    }

    [Fact]
    public void GivenOneTab_WhenClickingThePlusAfterIt_ThenTheSourcePickerOpens()
    {
        var state = Loaded(ViewLayout.List, 80, 24, "1:A-1");
        var button = TabBar.NewTabButton(state.Session.Tabs, state.Session.Active, 80)!.Value;

        var (result, _) = Resolve(state, 80, 24, button.Start, 0, MouseGesture.Click);

        Assert.NotNull(result.Picker);
    }

    [Fact]
    public void GivenTabBarWithoutRoomForThePlus_WhenBuilding_ThenThereIsNoButtonAndNoOverflow()
    {
        var state = TwoTabs(80);

        Assert.Null(TabBar.NewTabButton(state.Session.Tabs, state.Session.Active, 20));
        Assert.True(DisplayWidth.Of(TabBar.Build(state.Session.Tabs, state.Session.Active, 20).PlainText) <= 20);
    }

    [Fact]
    public void GivenScrolledTabBar_WhenClickingTheHiddenMarks_ThenTheNeighbouringHiddenTabActivates()
    {
        var state = Loaded(ViewLayout.List, 40, 24, "1:A-1");

        for (var index = 2; index <= 12; index++)
        {
            var definition = new ViewDefinition(ViewSourceKind.Jql, $"project = P{index}", ViewLayout.List, $"P{index}");

            state = _reducer.Reduce(state, new ViewOpenRequested(definition)).State;
        }

        var middle = _reducer.Reduce(state, new ActionRequested(ActionIds.TabGoTo6)).State;
        var areas = TabBar.Areas(middle.Session.Tabs, middle.Session.Active, 40);
        var tabs = middle.Session.Tabs;
        var first = areas[0];
        var last = areas[^1];
        var left = Resolve(middle, 40, 24, first.Start, 0, MouseGesture.Click).State;
        var right = Resolve(middle, 40, 24, last.Start, 0, MouseGesture.Click).State;

        Assert.Equal(first.Tab, left.Session.Active);
        Assert.Equal(last.Tab, right.Session.Active);
        Assert.NotEqual(tabs[0].Id, first.Tab);
        Assert.NotEqual(tabs[^1].Id, last.Tab);
    }

    [Fact]
    public void GivenOneColumnBoard_WhenClickingTheNeighbourNamesInTheHeader_ThenThatColumnIsSelected()
    {
        var state = Loaded(ViewLayout.Columns, 30, 20, "1:A-1", "2:A-2", "4:A-3");
        var centre = _reducer.Reduce(state, new ActionRequested(ActionIds.NavigateRight)).State;

        var left = Click(centre, 30, 20, 4, HeaderRows + 1);
        var right = Click(centre, 30, 20, 25, HeaderRows + 1);
        var title = Click(centre, 30, 20, 14, HeaderRows + 1);

        Assert.Equal(0, Active(left).Cursor!.Column);
        Assert.Equal(2, Active(right).Cursor!.Column);
        Assert.Equal(1, Active(title).Cursor!.Column);
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

    [Fact]
    public void GivenDoubleClickOnEmptySpace_WhenResolving_ThenNothingOpensAndTheSelectionStays()
    {
        var state = Loaded(ViewLayout.List, 80, 24, "1:A-1", "1:A-2");

        var (result, effects) = Resolve(state, 80, 24, 2, HeaderRows + ListHeader + 10, MouseGesture.DoubleClick);

        Assert.Empty(Active(result).Detail);
        Assert.Empty(effects);
        Assert.Equal("A-1", Active(result).SelectedKey);
    }

    [Fact]
    public void GivenRightClickOnEmptySpace_WhenResolving_ThenThePaletteStaysClosed()
    {
        var state = Loaded(ViewLayout.List, 80, 24, "1:A-1");

        var (result, _) = Resolve(state, 80, 24, 2, HeaderRows + ListHeader + 10, MouseGesture.RightClick);

        Assert.Equal(InputMode.Normal, result.Mode);
    }

    [Fact]
    public void GivenPendingCountAndChord_WhenClickingARow_ThenTheyAreDropped()
    {
        var state = Loaded(ViewLayout.List, 80, 24, "1:A-1", "1:A-2", "1:A-3");
        var typed = _reducer.Reduce(_reducer.Reduce(state, new KeyPressed("5")).State, new KeyPressed("Space")).State;

        var clicked = Click(typed, 80, 24, 2, HeaderRows + ListHeader + 1);

        Assert.Equal(string.Empty, clicked.PendingKeys);
        Assert.Equal(0, clicked.Count);
        Assert.Equal("A-2", Active(clicked).SelectedKey);
    }

    [Fact]
    public void GivenPendingChord_WhenClickingATab_ThenItIsDropped()
    {
        var state = TwoTabs(80);
        var typed = _reducer.Reduce(state, new KeyPressed("Space")).State;
        var areas = TabBar.Areas(typed.Session.Tabs, typed.Session.Active, 80);

        var clicked = Resolve(typed, 80, 24, areas[0].Start, 0, MouseGesture.Click).State;

        Assert.Equal(string.Empty, clicked.PendingKeys);
    }

    [Fact]
    public void GivenMoreColumnsThanFit_WhenClickingTheFirstDrawnColumn_ThenItsOwnColumnIsSelected()
    {
        var configuration = new BoardConfiguration(
            4,
            "Board",
            BoardType.Kanban,
            [.. Enumerable.Range(1, 6).Select(number => new BoardColumn($"C{number}", [number.ToString()]))],
            "10001",
            null,
            null,
            null);
        var state = LoadedWith(
            configuration,
            ViewLayout.Columns,
            80,
            30,
            ["1:A-1", "2:A-2", "3:A-3", "4:A-4", "5:A-5", "6:A-6"]);
        var last = _reducer.Reduce(state, new ActionRequested(ActionIds.NavigateLastColumn)).State;

        var first = Click(last, 80, 30, 5, HeaderRows + 2);
        var lastDrawn = Click(last, 80, 30, 75, HeaderRows + 2);

        Assert.Equal("A-3", Active(first).SelectedKey);
        Assert.Equal("A-6", Active(lastDrawn).SelectedKey);
    }

    [Fact]
    public void GivenScrolledColumn_WhenClickingTheFirstShownCard_ThenTheCardOfTheScrollIsSelected()
    {
        var keys = Enumerable.Range(1, 30).Select(number => $"1:A-{number}").ToArray();
        var state = Loaded(ViewLayout.Columns, 90, 30, keys);
        var bottom = _reducer.Reduce(state, new ActionRequested(ActionIds.NavigateBottom)).State;
        var scroll = Active(bottom).Cursor!.Scrolls[0];

        var result = Click(bottom, 90, 30, 5, HeaderRows + 2);

        Assert.True(scroll > 0);
        Assert.Equal($"A-{scroll + 1}", Active(result).SelectedKey);
    }

    [Fact]
    public void GivenWideCharacterTabTitles_WhenClickingTheSecondTab_ThenItActivates()
    {
        var state = Loaded(ViewLayout.List, 80, 24, "1:A-1");
        var definition = new ViewDefinition(ViewSourceKind.Jql, "summary ~ \"日本語日本語\"", ViewLayout.List, "日本語日本語");
        var two = _reducer.Reduce(state, new ViewOpenRequested(definition)).State;
        var three = _reducer.Reduce(two, new ViewOpenRequested(definition)).State;
        var areas = TabBar.Areas(three.Session.Tabs, three.Session.Active, 80);

        var (result, _) = Resolve(three, 80, 24, areas[2].Start, 0, MouseGesture.Click);

        Assert.Equal(three.Session.Tabs[2].Id, result.Session.Active);
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

    private ReduceResult Drag(
        AppState state,
        int width,
        int height,
        int fromColumn,
        int fromRow,
        int toColumn,
        int toRow)
    {
        var context = MouseContext.From(state, width, height, HeaderRows, _now);
        var current = new ReduceResult(state, []);

        foreach (var command in MouseResolver.ResolveDrag(context, fromColumn, fromRow, toColumn, toRow))
        {
            var next = _reducer.Reduce(current.State, command);

            current = new ReduceResult(next.State, [.. current.Effects, .. next.Effects]);
        }

        return current;
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
        return LoadedWith(_configuration, layout, width, height, specs);
    }

    private AppState LoadedWith(
        BoardConfiguration configuration,
        ViewLayout layout,
        int width,
        int height,
        string[] specs)
    {
        var rows = specs.Select(Issue).ToArray();
        var source = layout == ViewLayout.Columns ? ViewSourceKind.Board : ViewSourceKind.Jql;
        var definition = new ViewDefinition(source, source == ViewSourceKind.Board ? "4" : "project = A", layout, "View");
        var sized = _reducer.Reduce(Start(), new ViewportChanged(height - HeaderRows - 1 - ListHeader, width)).State;
        var opened = _reducer.Reduce(sized, new ViewOpenRequested(definition)).State;
        var tab = Active(opened);
        var configured = layout == ViewLayout.Columns
            ? _reducer.Reduce(opened, new BoardConfigurationLoaded(tab.Id, tab.RequestId, configuration)).State
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
