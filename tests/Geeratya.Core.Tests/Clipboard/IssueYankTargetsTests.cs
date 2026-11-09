using Geeratya.Core.Backlog;
using Geeratya.Core.Clipboard;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Tests.Backlog;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Clipboard;

/// <summary>Where the yank keys find their issue outside the detail (#896): board card, sprint header, which-key.</summary>
[Trait("Os", "Independent")]
public class IssueYankTargetsTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenTheColumnsLayout_WhenYankingKeyAndSummary_ThenTheCardUnderTheCursorIsCopied()
    {
        var state = Run(ColumnsBoard(("1", "A-1"), ("2", "A-2")), "l").State;

        var result = Run(state, "Space", "y", "s");

        Assert.Equal("A-2 Summary A-2", Assert.IsType<CopyToClipboardEffect>(Assert.Single(result.Effects)).Text);
    }

    [Fact]
    public void GivenTheColumnsLayoutOnAnEmptyColumn_WhenYanking_ThenNothingIsCopiedAndTheStatusWarns()
    {
        var state = Run(ColumnsBoard(("1", "A-1")), "l", "l").State;

        var result = Run(state, "Space", "y", "y");

        Assert.Empty(result.Effects);
        Assert.Contains("No issue selected", result.State.Message);
    }

    [Fact]
    public void GivenTheCursorOnAnEmptySprintHeader_WhenYanking_ThenNothingIsCopiedAndTheStatusWarns()
    {
        var tab = BacklogCursor.FocusSprint(
            BacklogFixtures.Tab(
                BacklogFixtures.RankedRows,
                [BacklogFixtures.Active, BacklogFixtures.Future, BacklogFixtures.EmptyFuture]),
            BacklogFixtures.EmptyFutureId);
        var session = SiteSession.Empty(_work) with { Tabs = [tab], Active = tab.Id };
        var state = AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_work), Session = session };

        var result = Run(state, "Space", "y", "y");

        Assert.Equal(BacklogFixtures.EmptyFutureId, tab.FocusedSprintId);
        Assert.Empty(result.Effects);
        Assert.Contains("No issue selected", result.State.Message);
    }

    [Fact]
    public void GivenTheDefaultKeymap_WhenListingTheYankGroup_ThenItIsLabelledYankNotIssue()
    {
        var rows = WhichKeyBuilder.Build(DefaultKeymap.Load(), InputMode.Normal, "Space");

        var group = rows.Single(row => row.Key == "y");

        Assert.Equal("+yank (3)", group.Label);
        Assert.Contains(rows, row => row.Key == "i" && row.Label.StartsWith("+issue", StringComparison.Ordinal));
    }

    private ReduceResult Run(AppState state, params string[] keys)
    {
        var result = new ReduceResult(state, []);
        var effects = new List<Effect>();

        foreach (var key in keys)
        {
            result = _reducer.Reduce(result.State, new KeyPressed(key));
            effects.AddRange(result.Effects);
        }

        return new ReduceResult(result.State, effects);
    }

    private AppState ColumnsBoard(params (string Status, string Key)[] cards)
    {
        var configuration = new BoardConfiguration(
            4,
            "Board",
            BoardType.Kanban,
            [new BoardColumn("To Do", ["1"]), new BoardColumn("Doing", ["2"]), new BoardColumn("Empty", ["3"])],
            "10001",
            null,
            null,
            null);
        var start = AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(_work),
            Session = SiteSession.Empty(_work),
        };
        var definition = new ViewDefinition(ViewSourceKind.Board, "4", ViewLayout.Columns, "Board");
        var opened = _reducer.Reduce(start, new ViewOpenRequested(definition)).State;
        var sized = _reducer.Reduce(opened, new ViewportChanged(20)).State;
        var tab = sized.Session.Tabs.Single(candidate => candidate.Id == sized.Session.Active);
        var configured = _reducer.Reduce(sized, new BoardConfigurationLoaded(tab.Id, tab.RequestId, configuration)).State;
        var page = new IssuePage([.. cards.Select(card => Card(card.Status, card.Key))], null, null);

        return _reducer.Reduce(configured, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
    }

    private static IssueSummary Card(string status, string key)
    {
        return new IssueSummary(
            key,
            key,
            $"Summary {key}",
            new IssueStatus(status, "Status", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
    }
}
