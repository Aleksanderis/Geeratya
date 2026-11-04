using System.Text.RegularExpressions;
using Geeratya.Core.Flash;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>The flash jump labels drawn by the columns and the Scrum backlog renderers (#661).</summary>
[Trait("Os", "Independent")]
public class FlashViewsOverlayTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

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
    public void GivenFullCardsInTwoColumns_WhenRendering_ThenTheLetterTakesTheCellBeforeTheTypeNameOfEachMatch()
    {
        var state = Board("A-", 30, Card("1", "A-1"), Card("2", "A-2"), Card("4", "B-3"));

        var lines = Frame(state, 120, 30);

        Assert.Matches(new Regex("[a-z]Task A-1"), Joined(lines));
        Assert.Matches(new Regex("[a-z]Task A-2"), Joined(lines));
        Assert.Matches(new Regex(" Task B-3"), Joined(lines));
        Assert.All(lines, line => Assert.Equal(120, DisplayWidth.Of(line)));
    }

    [Fact]
    public void GivenCompactCards_WhenRendering_ThenTheLetterReplacesTheCursorMarkCell()
    {
        var state = Board("A-", 8, Card("1", "A-1"), Card("2", "A-2"));

        var lines = Frame(state, 120, 8);

        Assert.Matches(new Regex("[a-z]T A-1"), Joined(lines));
        Assert.Matches(new Regex("[a-z]T A-2"), Joined(lines));
        Assert.All(lines, line => Assert.Equal(120, DisplayWidth.Of(line)));
    }

    [Fact]
    public void GivenNoFlashPrompt_WhenRendering_ThenTheCardsAreDrawnAsBefore()
    {
        var state = Board("A-", 30, Card("1", "A-1")) with { FlashJump = null, Mode = InputMode.Normal };

        var lines = Frame(state, 120, 30);

        Assert.DoesNotMatch(new Regex("[a-z]Task A-1"), Joined(lines));
    }

    [Fact]
    public void GivenAScrumBacklog_WhenRendering_ThenTheRowsOfEverySectionGetTheLetterAfterTheMatch()
    {
        var state = Backlog("Sum");

        var lines = Frame(state, 100, 24);

        Assert.Matches(new Regex("Sum[a-z]ary one"), Joined(lines));
        Assert.Matches(new Regex("Sum[a-z]ary two"), Joined(lines));
        Assert.All(lines, line => Assert.Equal(100, DisplayWidth.Of(line)));
    }

    private static string Joined(IReadOnlyList<string> lines)
    {
        return string.Join('\n', lines);
    }

    private static List<string> Frame(AppState state, int width, int height)
    {
        return [.. FrameBuilder.Build(state, width, height, _keymap).Skip(FrameBuilder.HeaderRows)];
    }

    private static AppState Board(string text, int height, params IssueSummary[] rows)
    {
        var definition = new ViewDefinition(ViewSourceKind.Board, "4", ViewLayout.Columns, "Board");
        var tab = TabState.Opened(new TabId(1), definition, 1, 1, 10) with
        {
            Load = LoadState.Idle,
            Rows = rows,
            Board = _configuration,
            SelectedKey = rows[0].Key,
            KeyWidth = 3,
            VisibleRows = height - ListLayout.HeaderRows - ListLayout.FooterRows,
        };

        return Flash(tab, text);
    }

    private static AppState Backlog(string text)
    {
        var sprint = new Sprint(7, "Sprint 1", SprintState.Active, null, null, null, null, 6);
        var definition = new ViewDefinition(ViewSourceKind.ScrumBacklog, "6", ViewLayout.List, "Scrum board");
        var tab = TabState.Opened(new TabId(1), definition, 1, 1, 10) with
        {
            Load = LoadState.Idle,
            Rows = [Row("A-1", "Summary one", sprint), Row("A-2", "Summary two", null)],
            KeyWidth = 3,
            Sprints = [sprint],
        };

        return Flash(tab, text);
    }

    private static AppState Flash(TabState tab, string text)
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");

        return AppState.Initial with
        {
            Mode = InputMode.Insert,
            Profiles = ProfileCatalog.Empty.With(profile),
            Session = SiteSession.Empty(profile) with { Tabs = [tab], Active = tab.Id },
            ViewportColumns = 120,
            FlashJump = new FlashJumpState(text),
        };
    }

    private static IssueSummary Card(string status, string key)
    {
        return Row(key, $"Summary {key}", null) with { Status = new IssueStatus(status, "Status", "new") };
    }

    private static IssueSummary Row(string key, string summary, Sprint? sprint)
    {
        IssueSprint[] sprints = sprint is null ? [] : [new IssueSprint(sprint.Id, sprint.Name, sprint.State)];

        return new IssueSummary(
            key,
            key,
            summary,
            new IssueStatus("1", "To Do", "new"),
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
