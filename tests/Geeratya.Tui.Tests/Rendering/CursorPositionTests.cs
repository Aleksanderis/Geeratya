using Geeratya.Core.Backlog;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class CursorPositionTests
{
    private const string GlobalHints = "? help  : commands  q quit";

    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly Sprint _active = new(7, "Sprint 1", SprintState.Active, null, null, null, null, 6);

    private static readonly BoardConfiguration _configuration = new(
        4,
        "Board",
        BoardType.Kanban,
        [new BoardColumn("To Do", ["1"]), new BoardColumn("Doing", ["2"]), new BoardColumn("Done", ["4"])],
        "10001",
        null,
        null,
        null);

    [Fact]
    public void GivenAListWithTheCursorOnTheFifthOfForty_WhenGettingTheText_ThenItIsFiveOfForty()
    {
        var tab = ListTab(40) with { Selected = 4 };

        Assert.Equal("5/40", CursorPosition.Text(tab));
    }

    [Fact]
    public void GivenAListWithMorePages_WhenGettingTheText_ThenTheTotalCarriesAPlus()
    {
        var tab = ListTab(40) with { Selected = 39, Next = new PageCursor("next") };

        Assert.Equal("40/40+", CursorPosition.Text(tab));
    }

    [Fact]
    public void GivenATruncatedList_WhenGettingTheText_ThenTheTotalCarriesAPlus()
    {
        var tab = ListTab(3) with { Truncated = true };

        Assert.Equal("1/3+", CursorPosition.Text(tab));
    }

    [Fact]
    public void GivenAnEmptyList_WhenGettingTheText_ThenThereIsNoPosition()
    {
        Assert.Equal(string.Empty, CursorPosition.Text(ListTab(0)));
    }

    [Fact]
    public void GivenAScrumBacklogWithSections_WhenGettingTheText_ThenTheRowCountsAcrossAllSections()
    {
        var tab = BacklogTab() with { Selected = 2 };

        Assert.Equal("3/4", CursorPosition.Text(tab));
    }

    [Fact]
    public void GivenTheCursorOnACollapsedBacklogSection_WhenGettingTheText_ThenThereIsNoPosition()
    {
        var tab = BacklogTab() with { Selected = 0, CollapsedSections = [BacklogSections.SprintKey(7)] };

        Assert.Equal(string.Empty, CursorPosition.Text(tab));
    }

    [Fact]
    public void GivenABoard_WhenGettingTheText_ThenItNamesTheColumnAndTheCard()
    {
        Assert.Equal("col 1/3  card 2/3", CursorPosition.Text(BoardTab("A-2")));
        Assert.Equal("col 2/3  card 1/1", CursorPosition.Text(BoardTab("A-4")));
    }

    [Fact]
    public void GivenABoardWithMorePages_WhenGettingTheText_ThenTheCardTotalCarriesAPlus()
    {
        var tab = BoardTab("A-1") with { Next = new PageCursor("next") };

        Assert.Equal("col 1/3  card 1/3+", CursorPosition.Text(tab));
    }

    [Theory]
    [InlineData(120)]
    [InlineData(80)]
    [InlineData(40)]
    public void GivenAListInTheStatusBar_WhenRenderingAtEveryWidth_ThenTheGlobalHintsStayAndThePositionShowsWhereThereIsRoom(int width)
    {
        var line = StatusBar.Render(ActiveOf(ListTab(40) with { Selected = 4 }), width, _keymap);

        Assert.Equal(width, line.Length);
        Assert.EndsWith(GlobalHints, line);

        if (width >= 80)
        {
            Assert.Contains("5/40", line);
        }
    }

    [Fact]
    public void GivenABoardAt80Columns_WhenRendering_ThenThePositionShowsBeforeTheBoardTextAndTheGlobalHintsStay()
    {
        var line = StatusBar.Render(ActiveOf(BoardTab("A-2")), 80, _keymap);

        Assert.Contains("col 1/3  card 2/3 · ", line);
        Assert.EndsWith(GlobalHints, line);
        Assert.Equal(80, line.Length);
    }

    [Fact]
    public void GivenAnyWidth_WhenRenderingABoardAndABacklog_ThenTheLineKeepsTheWidth()
    {
        for (var width = 1; width <= 130; width++)
        {
            Assert.Equal(width, StatusBar.Render(ActiveOf(BoardTab("A-2")), width, _keymap).Length);
            Assert.Equal(width, StatusBar.Render(ActiveOf(BacklogTab()), width, _keymap).Length);
        }
    }

    private static AppState ActiveOf(TabState tab)
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var session = SiteSession.Empty(profile) with { Tabs = [tab], Active = tab.Id };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(profile), Session = session };
    }

    private static TabState ListTab(int count)
    {
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "A");
        var rows = Enumerable.Range(1, count).Select(number => Issue($"A-{number}", "1", null)).ToList();

        return TabState.Opened(new TabId(1), definition, 1, 1, 20) with
        {
            Rows = rows,
            Selected = 0,
            SelectedKey = count == 0 ? null : "A-1",
            KeyWidth = 4,
            Load = LoadState.Idle,
        };
    }

    private static TabState BacklogTab()
    {
        var definition = new ViewDefinition(ViewSourceKind.ScrumBacklog, "6", ViewLayout.List, "Scrum board");
        var rows = BacklogSections.Order(
        [
            Issue("A-5", "1", null),
            Issue("A-3", "1", _active),
            Issue("A-4", "1", _active),
            Issue("A-6", "1", null),
        ]);

        return TabState.Opened(new TabId(1), definition, 1, 1, 20) with
        {
            Rows = rows,
            Selected = 0,
            SelectedKey = rows[0].Key,
            KeyWidth = 3,
            Load = LoadState.Idle,
            Sprints = [_active],
        };
    }

    private static TabState BoardTab(string selectedKey)
    {
        var definition = new ViewDefinition(ViewSourceKind.Board, "4", ViewLayout.Columns, "Board");
        var rows = new[]
        {
            Issue("A-1", "1", null),
            Issue("A-2", "1", null),
            Issue("A-3", "1", null),
            Issue("A-4", "2", null),
        };

        return TabState.Opened(new TabId(1), definition, 1, 1, 10) with
        {
            Load = LoadState.Idle,
            Rows = rows,
            Board = _configuration,
            SelectedKey = selectedKey,
            KeyWidth = 3,
        };
    }

    private static IssueSummary Issue(string key, string statusId, Sprint? sprint)
    {
        IssueSprint[] sprints = sprint is null ? [] : [new IssueSprint(sprint.Id, sprint.Name, sprint.State)];

        return new IssueSummary(
            key,
            key,
            "Summary",
            new IssueStatus(statusId, "Status", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A")
        {
            Sprints = sprints,
        };
    }
}
