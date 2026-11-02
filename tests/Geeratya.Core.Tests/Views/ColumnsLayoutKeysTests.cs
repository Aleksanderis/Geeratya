using System.Globalization;
using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Views;

[Trait("Os", "Independent")]
public class ColumnsLayoutKeysTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private static readonly BoardConfiguration _configuration = new(
        4,
        "Board",
        BoardType.Kanban,
        [
            new BoardColumn("To Do", ["1"]),
            new BoardColumn("Doing", ["2"]),
            new BoardColumn("Empty", ["3"]),
            new BoardColumn("Done", ["4"]),
        ],
        "10001",
        null,
        null,
        null);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenBoardTab_WhenPressingLAndH_ThenTheSelectionMovesAcrossColumnsAndKeepsTheRow()
    {
        var state = Loaded(Issues("1:A-1", "1:A-2", "2:A-3", "2:A-4", "4:A-5"));
        var down = Press(state, "j");

        var right = Press(down, "l");
        var back = Press(right, "h");

        Assert.Equal("A-4", Tab(right).SelectedKey);
        Assert.Equal(1, Tab(right).Cursor!.Column);
        Assert.Equal("A-2", Tab(back).SelectedKey);
        Assert.Equal(0, Tab(back).Cursor!.Column);
    }

    [Fact]
    public void GivenBoardTab_WhenPressingDollarAndZero_ThenFirstAndLastColumnAreTab()
    {
        var state = Loaded(Issues("1:A-1", "4:A-2"));

        var last = Press(state, "$");
        var first = Press(last, "0");

        Assert.Equal("A-2", Tab(last).SelectedKey);
        Assert.Equal(3, Tab(last).Cursor!.Column);
        Assert.Equal("A-1", Tab(first).SelectedKey);
    }

    [Fact]
    public void GivenEmptyColumn_WhenMovingThroughIt_ThenItIsSelectedAndTheNextColumnKeepsTheRowIndex()
    {
        var state = Loaded(Issues("2:A-1", "2:A-2", "4:A-3", "4:A-4"));
        var inDoing = Press(state, "j");

        var empty = Press(inDoing, "l");
        var done = Press(empty, "l");

        Assert.Equal(2, Tab(empty).Cursor!.Column);
        Assert.Null(ColumnsNavigation.SelectedIssue(Tab(empty)));
        Assert.Equal("A-4", Tab(done).SelectedKey);
    }

    [Fact]
    public void GivenLongColumn_WhenPressingDownBottomTopAndPages_ThenItActsInsideTheColumn()
    {
        var rows = Enumerable.Range(1, 30).Select(number => $"1:A-{number}").Append("2:B-1").ToArray();
        var state = Loaded(Issues(rows));

        var bottom = Press(state, "G");
        var pageUp = Press(bottom, "Ctrl-u");
        var top = Press(Press(Press(pageUp, "g"), "g"), "j");
        var pageDown = Press(top, "Ctrl-d");

        Assert.Equal("A-30", Tab(bottom).SelectedKey);
        Assert.Equal(0, Tab(bottom).Cursor!.Column);
        Assert.True(Tab(pageUp).Cursor!.Card < 29);
        Assert.Equal("A-2", Tab(top).SelectedKey);
        Assert.True(Tab(pageDown).Cursor!.Card > 1);
        Assert.True(Tab(bottom).Cursor!.Scrolls[0] > 0);
    }

    [Fact]
    public void GivenEmptyColumnSelected_WhenPressingJ_ThenNothingMoves()
    {
        var state = Loaded(Issues("1:A-1", "1:A-2"));
        var empty = Press(Press(state, "l"), "l");

        var down = Press(empty, "j");

        Assert.Equal(Tab(empty).Cursor!.Column, Tab(down).Cursor!.Column);
        Assert.Equal(0, Tab(down).Cursor!.Card);
    }

    [Fact]
    public void GivenCardAndEmptyColumn_WhenPressingSpaceO_ThenTheCardOpensAndTheEmptyColumnOpensNothing()
    {
        var state = Loaded(Issues("1:A-1", "1:A-2"));
        var emptyColumn = Press(Press(state, "l"), "l");

        var onCard = Press(Press(state, "Space"), "o");
        var onEmpty = Press(Press(emptyColumn, "Space"), "o");

        Assert.Equal("Opening in the browser...", onCard.Message);
        Assert.StartsWith("No issue selected", onEmpty.Message);
    }

    [Fact]
    public void GivenCardAndEmptyColumn_WhenPressingEnter_ThenTheCardOpensAndTheEmptyColumnOpensNothing()
    {
        var state = Loaded(Issues("1:A-1", "1:A-2"));
        var emptyColumn = Press(Press(state, "l"), "l");

        var onCard = Press(state, "Enter");
        var onEmpty = Press(emptyColumn, "Enter");

        Assert.Single(Tab(onCard).Detail);
        Assert.Empty(Tab(onEmpty).Detail);
    }

    [Fact]
    public void GivenCardInSecondColumn_WhenPressingEnter_ThenThatIssueDetailLoadsAndEmptyColumnStartsNoLoad()
    {
        var state = Press(Loaded(Issues("1:A-1", "2:A-2")), "l");
        var empty = Press(state, "l");

        var onCard = _reducer.Reduce(state, new KeyPressed("Enter"));
        var onEmpty = _reducer.Reduce(empty, new KeyPressed("Enter"));

        Assert.Equal("A-2", Tab(onCard.State).Detail.Single().Key);
        Assert.Equal("A-2", Assert.Single(onCard.Effects.OfType<LoadIssueDetailEffect>()).Key);
        Assert.Empty(onEmpty.Effects);
        Assert.Equal(empty, onEmpty.State);
    }

    [Fact]
    public void GivenCardAndEmptyColumn_WhenPressingSpaceO_ThenTheCardBrowseUrlOpensAndEmptyColumnOpensNothing()
    {
        var state = Press(Loaded(Issues("1:A-1", "2:A-2")), "l");
        var empty = Press(state, "l");

        var onCard = _reducer.Reduce(Press(state, "Space"), new KeyPressed("o"));
        var onEmpty = _reducer.Reduce(Press(empty, "Space"), new KeyPressed("o"));

        var effect = Assert.IsType<OpenUrlEffect>(Assert.Single(onCard.Effects));
        Assert.Equal("https://work.atlassian.net/browse/A-2", effect.Url.AbsoluteUri);
        Assert.Empty(onEmpty.Effects);
    }

    [Fact]
    public void GivenListLayout_WhenPressingColumnKeys_ThenNothingHappens()
    {
        var state = Press(Loaded(Issues("1:A-1", "2:A-2")), "Space");
        var list = Press(state, "l");

        var right = Press(list, "l");
        var last = Press(right, "$");

        Assert.Equal(ViewLayout.List, Tab(last).Definition.Layout);
        Assert.Equal(0, Tab(last).Selected);
        Assert.Same(Tab(list), Tab(last));
    }

    [Fact]
    public void GivenColumnsSelection_WhenTogglingToListAndBack_ThenTheSameIssueStaysTab()
    {
        var state = Press(Loaded(Issues("1:A-1", "2:A-2", "2:A-3")), "l");
        var moved = Press(state, "j");

        var list = Toggle(moved);
        var back = Toggle(list);

        Assert.Equal(ViewLayout.List, Tab(list).Definition.Layout);
        Assert.Equal("A-3", Tab(list).SelectedKey);
        Assert.Equal(2, Tab(list).Selected);
        Assert.Equal(ViewLayout.Columns, Tab(back).Definition.Layout);
        Assert.Equal("A-3", ColumnsNavigation.SelectedIssue(Tab(back))?.Key);
    }

    [Fact]
    public void GivenJqlTab_WhenTogglingLayout_ThenTheStatusLineSaysThereAreNoColumns()
    {
        var opened = Open(ViewSourceKind.Jql, ViewLayout.List);

        var result = Toggle(opened);

        Assert.Equal("This view has no columns layout", result.Message);
        Assert.Equal(ViewLayout.List, Tab(result).Definition.Layout);
    }

    [Fact]
    public void GivenBacklogTab_WhenRunningLayoutColumns_ThenItIsRefused()
    {
        var opened = Open(ViewSourceKind.Backlog, ViewLayout.List);

        var result = Run(opened, "layout columns");

        Assert.Equal("This view has no columns layout", result.Message);
    }

    [Fact]
    public void GivenBoardTab_WhenRunningLayoutCommands_ThenTheLayoutSwitchesAndBadArgumentsSayHow()
    {
        var state = Loaded(Issues("1:A-1"));

        var list = Run(state, "layout list");
        var columns = Run(list, "layout columns");
        var bad = Run(columns, "layout grid");

        Assert.Equal(ViewLayout.List, Tab(list).Definition.Layout);
        Assert.Equal(ViewLayout.Columns, Tab(columns).Definition.Layout);
        Assert.Equal("Usage: :layout list|columns", bad.Message);
    }

    [Fact]
    public void GivenColumnsLayoutNoRows_WhenPressingKeys_ThenNothingThrows()
    {
        var opened = Open(ViewSourceKind.Board, ViewLayout.Columns);

        var result = Press(Press(Press(Press(opened, "l"), "j"), "$"), "G");

        Assert.Equal(ViewLayout.Columns, Tab(result).Definition.Layout);
    }

    [Fact]
    public void GivenRefreshReordersTheColumn_WhenResolving_ThenTheSelectedIssueIsStillTheSame()
    {
        var state = Press(Loaded(Issues("1:A-1", "1:A-2", "1:A-3")), "j");
        var tab = Tab(state);
        var reordered = tab with { Rows = Issues("1:A-3", "1:A-9", "1:A-2", "1:A-1") };

        Assert.Equal("A-2", ColumnsNavigation.SelectedIssue(reordered)?.Key);
    }

    private static TabState Tab(AppState state)
    {
        return state.Session.Tabs.Single(candidate => candidate.Id == state.Session.Active);
    }

    private static AppState Start()
    {
        return AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(_work),
            Session = SiteSession.Empty(_work),
        };
    }

    private static IssueSummary[] Issues(params string[] specs)
    {
        return [.. specs.Select(Issue)];
    }

    /// <summary>A spec is <c>statusId:key</c>.</summary>
    private static IssueSummary Issue(string spec)
    {
        var parts = spec.Split(':');

        return new IssueSummary(
            parts[1],
            parts[1],
            $"Summary {parts[1]}",
            new IssueStatus(parts[0], "Status", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
    }

    private AppState Open(ViewSourceKind source, ViewLayout layout)
    {
        var definition = new ViewDefinition(source, "4", layout, "Board");
        var opened = _reducer.Reduce(Start(), new ViewOpenRequested(definition)).State;

        return _reducer.Reduce(opened, new ViewportChanged(20)).State;
    }

    private AppState Loaded(IssueSummary[] rows, BoardConfiguration? configuration = null)
    {
        var opened = Open(ViewSourceKind.Board, ViewLayout.Columns);
        var tab = Tab(opened);
        var configured = _reducer.Reduce(opened, new BoardConfigurationLoaded(tab.Id, tab.RequestId, configuration ?? _configuration));
        var page = new IssuePage(rows, null, rows.Length);

        return _reducer.Reduce(configured.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
    }

    private AppState Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key)).State;
    }

    private AppState Toggle(AppState state)
    {
        return Press(Press(state, "Space"), "l");
    }

    private AppState Run(AppState state, string command)
    {
        var result = Press(state, ":");

        foreach (var character in command)
        {
            result = Press(result, character == ' ' ? "Space" : character.ToString(CultureInfo.InvariantCulture));
        }

        return Press(result, "Enter");
    }
}
