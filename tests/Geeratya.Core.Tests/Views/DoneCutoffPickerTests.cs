using Geeratya.Core.Boards;
using Geeratya.Core.CommandPalette;
using Geeratya.Core.Keymap;
using Geeratya.Core.Menus;
using Geeratya.Core.Profiles;
using Geeratya.Core.Settings;
using Geeratya.Core.Startup;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Views;

[Trait("Os", "Independent")]
public class DoneCutoffPickerTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private static readonly BoardConfiguration _configuration = new(
        4,
        "Board",
        BoardType.Kanban,
        [new BoardColumn("To Do", ["1"]), new BoardColumn("Done", ["4"])],
        "10001",
        null,
        null,
        null);

    private readonly KeymapTable _keymap = DefaultKeymap.Load();
    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenAKanbanBoard_WhenPressingTheKey_ThenThePaletteOpensOnTheDoneCutoffRows()
    {
        var state = Kanban(14, DoneCutoffSource.Jira);

        var result = PressKeys(state, "Space", "v", "d");

        Assert.Equal(InputMode.Command, result.State.Mode);
        Assert.Equal(DoneCutoffPicker.Prefix, result.State.PaletteText);
        var rows = PaletteRows.Selectable(result.State, _keymap).Where(row => row.Id.StartsWith("donecutoff")).ToList();
        Assert.Equal(
            ["donecutoff auto", "donecutoff 1w", "donecutoff 2w", "donecutoff 3w", "donecutoff 4w", "donecutoff all"],
            rows.Select(row => row.Id));
        Assert.Equal("Done cutoff: follow Jira (2 weeks, from Jira)", rows[0].Title);
        Assert.Equal("Done cutoff: 1 week", rows[1].Title);
        Assert.Equal("Done cutoff: all", rows[5].Title);
    }

    [Fact]
    public void GivenAChoiceMade_WhenOpeningThePicker_ThenThatRowIsMarkedCurrent()
    {
        var chosen = Run(Kanban(14, DoneCutoffSource.Jira), "donecutoff 3w").State;

        var rows = DoneCutoffPicker.Rows(chosen);

        Assert.Equal("Done cutoff: 3 weeks (current)", rows.Single(row => row.Id == "donecutoff 3w").Title);
        Assert.Equal("Done cutoff: follow Jira", rows[0].Title);
    }

    [Fact]
    public void GivenAKanbanBoard_WhenListingThePaletteWithoutAFilter_ThenThePickerRowsStayOut()
    {
        var rows = PaletteRows.For(Kanban(14, DoneCutoffSource.Jira), _keymap);

        Assert.DoesNotContain(rows, row => row.Id.StartsWith("donecutoff"));
        Assert.Contains(rows, row => row.Id == ActionIds.ViewDoneCutoff);
    }

    [Fact]
    public void GivenATabThatIsNotAKanbanBoard_WhenUsingTheKeyOrTheCommand_ThenTheyAreRefused()
    {
        var jql = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "Query");
        var opened = _reducer.Reduce(Start(), new ViewOpenRequested(jql)).State;
        var unloadedBoard = _reducer.Reduce(Start(), new ViewOpenRequested(BoardDefinition())).State;

        var key = PressKeys(opened, "Space", "v", "d");
        var command = Run(opened, "donecutoff 1w");
        var notYetKnown = Run(unloadedBoard, "donecutoff 1w");

        Assert.Equal(DoneCutoffPicker.NoBoardText, key.State.Message);
        Assert.Equal(InputMode.Normal, key.State.Mode);
        Assert.Equal(DoneCutoffPicker.NoBoardText, command.State.Message);
        Assert.Equal(DoneCutoffPicker.NoBoardText, notYetKnown.State.Message);
        Assert.Empty(command.Effects);
        Assert.Empty(DoneCutoffPicker.Rows(opened));
    }

    [Theory]
    [InlineData("1w", 7)]
    [InlineData("2w", 14)]
    [InlineData("3w", 21)]
    [InlineData("4w", 28)]
    [InlineData("all", 0)]
    public void GivenAChoice_WhenRunningTheCommand_ThenTheTabReloadsAndEveryLoadCarriesIt(string word, int days)
    {
        var state = Kanban(14, DoneCutoffSource.Jira);

        var result = Run(state, $"donecutoff {word}");

        var tab = Tab(result.State);
        var load = LoadOf(result);
        Assert.Equal(days, tab.DoneCutoffChoice);
        Assert.Equal(days, load.DoneCutoffChoice);
        Assert.Equal(LoadReason.Open, load.Reason);
        Assert.Empty(tab.Rows);
        Assert.Contains(result.Effects, effect => effect is CancelTabEffect);
        Assert.Contains("not saved in Jira", result.State.Message);
        var refresh = PressKeys(Loaded(result.State), "Space", "r");
        Assert.Equal(days, LoadOf(refresh).DoneCutoffChoice);
    }

    [Fact]
    public void GivenAChoice_WhenFollowingJiraAgain_ThenTheChoiceIsClearedAndTheBoardReloads()
    {
        var chosen = Run(Kanban(14, DoneCutoffSource.Jira), "donecutoff 1w").State;

        var result = Run(chosen, "donecutoff auto");

        Assert.Null(Tab(result.State).DoneCutoffChoice);
        Assert.Null(LoadOf(result).DoneCutoffChoice);
        Assert.Equal("Done cutoff follows Jira again.", result.State.Message);
    }

    [Fact]
    public void GivenTheChoiceAlreadyMade_WhenChoosingItAgain_ThenNothingReloads()
    {
        var chosen = Run(Kanban(14, DoneCutoffSource.Jira), "donecutoff 1w").State;

        var result = Run(chosen, "donecutoff 1w");

        Assert.Empty(result.Effects);
    }

    [Theory]
    [InlineData("donecutoff", "Done cutoff: 2 weeks, from Jira. Usage: :donecutoff [auto|1w|2w|3w|4w|all]")]
    [InlineData("donecutoff 5w", "Usage: :donecutoff [auto|1w|2w|3w|4w|all]")]
    [InlineData("donecutoff 10", "Usage: :donecutoff [auto|1w|2w|3w|4w|all]")]
    public void GivenNoOrABadArgument_WhenRunningTheCommand_ThenItSaysWhatIsAppliedOrTheUsage(
        string line,
        string message)
    {
        var result = Run(Kanban(14, DoneCutoffSource.Jira), line);

        Assert.Equal(message, result.State.Message);
        Assert.Empty(result.Effects);
    }

    [Theory]
    [InlineData(DoneCutoffSource.Jira, "2 weeks, from Jira")]
    [InlineData(DoneCutoffSource.Setting, "2 weeks, from the setting board.done-cutoff")]
    [InlineData(DoneCutoffSource.Choice, "2 weeks, your choice for this tab")]
    public void GivenEachSource_WhenDescribing_ThenTheTextSaysWhereTheValueCameFrom(
        DoneCutoffSource source,
        string expected)
    {
        var tab = Tab(Kanban(14, source));

        Assert.Equal(expected, DoneCutoffPicker.Describe(tab));
    }

    [Fact]
    public void GivenTwoBoardTabs_WhenChoosingOnOne_ThenTheOtherKeepsFollowingJira()
    {
        var first = Kanban(14, DoneCutoffSource.Jira);
        var second = _reducer.Reduce(first, new ViewOpenRequested(BoardDefinition("7"))).State;
        var secondTab = Tab(second);
        var secondApplied = _reducer.Reduce(second, new DoneCutoffApplied(secondTab.Id, secondTab.RequestId, 7, DoneCutoffSource.Jira));
        var firstTab = first.Session.Tabs[0];
        var switched = _reducer.Reduce(secondApplied.State, new TabActivated(firstTab.Id));

        var chosen = Run(switched.State, "donecutoff all").State;

        Assert.Equal(0, chosen.Session.Tabs.Single(tab => tab.Id == firstTab.Id).DoneCutoffChoice);
        Assert.Null(chosen.Session.Tabs.Single(tab => tab.Id == secondTab.Id).DoneCutoffChoice);
    }

    [Fact]
    public void GivenAChoiceOnATab_WhenItIsRememberedAndRestored_ThenItComesBackForThatBoard()
    {
        var chosen = Run(Kanban(14, DoneCutoffSource.Jira), "donecutoff 1w").State;
        var remembered = RememberedTabs.Of(chosen.Session);

        var (restored, _) = remembered.ToRestored();

        Assert.Equal(7, remembered.Items[0].DoneCutoff);
        Assert.Equal(7, restored[0].DoneCutoff);
        Assert.Equal("4", restored[0].Definition.SourceValue);
    }

    [Fact]
    public void GivenNoChoice_WhenRemembering_ThenNothingIsStored()
    {
        var remembered = RememberedTabs.Of(Kanban(14, DoneCutoffSource.Jira).Session);

        Assert.Null(remembered.Items[0].DoneCutoff);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(-1)]
    [InlineData(100)]
    public void GivenAnInvalidChoiceInTheFile_WhenRestoring_ThenTheTabFollowsJira(int days)
    {
        var item = new RememberedTab("Board", "4", "Columns", "Board", DoneCutoff: days);

        var (restored, _) = new RememberedTabs([item]).ToRestored();

        Assert.Null(restored[0].DoneCutoff);
    }

    [Fact]
    public void GivenARestoredChoice_WhenTheTabOpens_ThenTheFirstLoadCarriesIt()
    {
        var item = new RememberedTab("Board", "4", "Columns", "Board", DoneCutoff: 21);
        var begun = _reducer.Reduce(Start(), new StartupRequested()).State;

        var result = _reducer.Reduce(begun, new TabsLoaded(_work, new RememberedTabs([item])));

        Assert.Equal(21, Tab(result.State).DoneCutoffChoice);
        Assert.Equal(21, LoadOf(result).DoneCutoffChoice);
    }

    [Fact]
    public void GivenALoadedKanbanBoard_WhenTheViewMenuOpens_ThenItOffersThePicker()
    {
        var kanban = ContextMenus.ForView(Kanban(14, DoneCutoffSource.Jira));
        var unloaded = ContextMenus.ForView(_reducer.Reduce(Start(), new ViewOpenRequested(BoardDefinition())).State);

        Assert.Contains(kanban!.Items, item => item.Action == ActionIds.ViewDoneCutoff);
        Assert.DoesNotContain(unloaded!.Items, item => item.Action == ActionIds.ViewDoneCutoff);
    }

    [Theory]
    [InlineData("auto", null)]
    [InlineData("jira", null)]
    [InlineData("1w", 7)]
    [InlineData("4w", 28)]
    [InlineData("all", 0)]
    public void GivenAWord_WhenParsing_ThenItIsTheChoiceItNames(string word, int? expected)
    {
        Assert.True(DoneCutoff.TryParseChoice(word, out var days));
        Assert.Equal(expected, days);
    }

    [Theory]
    [InlineData(7, "1 week")]
    [InlineData(21, "3 weeks")]
    [InlineData(0, "all")]
    [InlineData(10, "10 days")]
    [InlineData(1, "1 day")]
    public void GivenDays_WhenLabelling_ThenWeeksAreUsedWhenTheyFit(int days, string label)
    {
        Assert.Equal(label, DoneCutoff.Label(days));
    }

    [Fact]
    public void GivenTheSetting_WhenTheChoiceIsNotMade_ThenTheLoadCarriesBothTheSettingAndNoChoice()
    {
        var state = Start() with { Settings = new AppSettings(DoneCutoffDays: 9) };

        var load = LoadOf(_reducer.Reduce(state, new ViewOpenRequested(BoardDefinition())));

        Assert.Equal(9, load.DoneCutoffDays);
        Assert.Null(load.DoneCutoffChoice);
    }

    [Fact]
    public void GivenTheFirstPageAppliedACutoff_WhenTheNextPageIsRequested_ThenOnlyThatLoadCarriesThePinnedDays()
    {
        var state = Start() with { Settings = new AppSettings(DoneCutoffDays: 9) };
        var opened = _reducer.Reduce(state, new ViewOpenRequested(BoardDefinition()));
        var tab = Tab(opened.State);
        var applied = _reducer.Reduce(
            opened.State,
            new DoneCutoffApplied(tab.Id, tab.RequestId, 14, DoneCutoffSource.Jira)).State;
        var page = new IssuePage([], new PageCursor("2"), null);
        var loaded = _reducer.Reduce(applied, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;

        var next = _reducer.Reduce(loaded, new NextPageRequested(tab.Id));

        Assert.Null(LoadOf(opened).PinnedCutoff);
        Assert.Equal(new PinnedCutoff(14, DoneCutoffSource.Jira), LoadOf(next).PinnedCutoff);
        Assert.Equal(9, LoadOf(next).DoneCutoffDays);
    }

    private static ViewDefinition BoardDefinition(string id = "4")
    {
        return new ViewDefinition(ViewSourceKind.Board, id, ViewLayout.Columns, "Board");
    }

    private static LoadViewPageEffect LoadOf(ReduceResult result)
    {
        return Assert.IsType<LoadViewPageEffect>(result.Effects.Single(effect => effect is LoadViewPageEffect));
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

    private AppState Kanban(int days, DoneCutoffSource source)
    {
        var opened = _reducer.Reduce(Start(), new ViewOpenRequested(BoardDefinition())).State;
        var sized = _reducer.Reduce(opened, new ViewportChanged(20)).State;
        var tab = Tab(sized);
        var applied = _reducer.Reduce(sized, new DoneCutoffApplied(tab.Id, tab.RequestId, days, source)).State;

        return Loaded(applied);
    }

    private AppState Loaded(AppState state)
    {
        var tab = Tab(state);
        var fresh = tab.Load is Loading ? state : _reducer.Reduce(state, new RefreshRequested(tab.Id)).State;
        var current = Tab(fresh);
        var configured = _reducer.Reduce(fresh, new BoardConfigurationLoaded(current.Id, current.RequestId, _configuration));
        var page = new IssuePage([], null, 0);

        return _reducer.Reduce(configured.State, new ViewPageLoaded(current.Id, current.RequestId, page, _now)).State;
    }

    private ReduceResult Run(AppState state, string line)
    {
        var typing = state with { Mode = InputMode.Command, PaletteText = line, PaletteBack = 0 };

        return _reducer.Reduce(typing, new KeyPressed("Enter"));
    }

    private ReduceResult PressKeys(AppState state, params string[] keys)
    {
        var result = new ReduceResult(state, []);

        foreach (var key in keys)
        {
            result = _reducer.Reduce(result.State, new KeyPressed(key));
        }

        return result;
    }
}
