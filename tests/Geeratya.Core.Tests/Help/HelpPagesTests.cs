using Geeratya.Core.Help;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Settings;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Help;

/// <summary>The pages the help overlay generates from the keymap and the action catalog.</summary>
[Trait("Os", "Independent")]
public class HelpPagesTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Fact]
    public void GivenNoTab_WhenListingThePages_ThenGlobalAllActionsAndCommandsComeInOrder()
    {
        var names = HelpPages.Names(Help(AppState.Initial), _keymap);

        Assert.Equal([HelpPages.GlobalName, HelpPages.AllName, HelpPages.CommandsName], names);
    }

    [Fact]
    public void GivenAListTab_WhenListingThePages_ThenTheListPageComesFirstAndHasNoColumnKeys()
    {
        var state = Help(Opened(ViewSourceKind.Jql, ViewLayout.List));

        var lines = HelpPages.Lines(state, _keymap);

        Assert.Equal("List", HelpPages.Names(state, _keymap)[0]);
        Assert.Contains(lines, line => line.Contains("Move down"));
        Assert.Contains(lines, line => line.Contains("Open the selected item"));
        Assert.DoesNotContain(lines, line => line.Contains("Move left"));
        Assert.DoesNotContain(lines, line => line.Contains("Next tab"));
    }

    [Fact]
    public void GivenAColumnsTab_WhenShowingTheFirstPage_ThenItHasTheColumnKeys()
    {
        var state = Help(Opened(ViewSourceKind.Board, ViewLayout.Columns));

        var lines = HelpPages.Lines(state, _keymap);

        Assert.Equal("Columns", HelpPages.Names(state, _keymap)[0]);
        Assert.Contains(lines, line => line.Contains("Move left"));
        Assert.Contains(lines, line => line.Contains("Move the card to the next column"));
    }

    [Fact]
    public void GivenABacklogTab_WhenShowingTheFirstPage_ThenItIsNamedBacklogAndHasRankKeys()
    {
        var state = Help(Opened(ViewSourceKind.Backlog, ViewLayout.List));

        var lines = HelpPages.Lines(state, _keymap);

        Assert.Equal("Backlog", HelpPages.Names(state, _keymap)[0]);
        Assert.Contains(lines, line => line.Contains("Rank the issue up"));
    }

    [Fact]
    public void GivenAnOpenedIssue_WhenShowingTheFirstPage_ThenItHasTheIssueDetailKeys()
    {
        var reducer = new AppReducer(_keymap);
        var opened = reducer.Reduce(Opened(ViewSourceKind.Jql, ViewLayout.List), new KeyPressed("Enter")).State;

        var state = Help(opened);
        var lines = HelpPages.Lines(state, _keymap);

        Assert.Equal("Issue detail", HelpPages.Names(state, _keymap)[0]);
        Assert.Contains(lines, line => line.Contains("Open the parent issue"));
        Assert.Contains(lines, line => line.Contains("Next section of the issue"));
        Assert.DoesNotContain(lines, line => line.Contains("Move left"));
    }

    [Fact]
    public void GivenTheGlobalPage_WhenShowing_ThenItHasTabsAndQuitButNoMotions()
    {
        var lines = HelpPages.Lines(HelpAt(0), _keymap);

        Assert.Contains(lines, line => line.Contains("Next tab"));
        Assert.Contains(lines, line => line.Contains("Quit"));
        Assert.DoesNotContain(lines, line => line.Contains("Move down"));
    }

    [Fact]
    public void GivenAnAction_WhenShowingARow_ThenTheMainKeyComesFirstAndTheOthersFollowTheTitle()
    {
        var line = HelpPages.Lines(HelpAt(0), _keymap).First(row => row.Contains("Next tab"));

        Assert.StartsWith("  Tab ", line);
        Assert.EndsWith("Next tab  (g t)", line);
    }

    [Fact]
    public void GivenTheAllActionsPage_WhenShowing_ThenActionsSitUnderTheCatalogGroups()
    {
        var lines = HelpPages.Lines(Advanced(HelpAt(1)), _keymap).ToList();

        foreach (var group in ActionCatalog.Groups)
        {
            Assert.Contains(group, lines);
        }

        Assert.True(lines.IndexOf("Issue") < lines.IndexOf("Comment"));
    }

    [Fact]
    public void GivenTheCatalog_WhenCheckingCompleteness_ThenEveryBoundActionIsOnTheAllActionsPage()
    {
        var lines = HelpPages.Lines(Advanced(HelpAt(1)), _keymap);
        var bound = _keymap.Bindings.Select(binding => binding.Action).Distinct().ToList();

        foreach (var action in bound)
        {
            var definition = ActionCatalog.All.Single(candidate => candidate.Id == action);
            var keys = _keymap.Bindings.First(binding => binding.Action == action).Keys;

            Assert.False(string.IsNullOrWhiteSpace(definition.Title), $"{action} has no title");
            Assert.True(Enum.IsDefined(definition.Level), $"{action} has no level");
            Assert.Contains(lines, line => line.Contains(definition.Title) && line.Contains(keys));
        }
    }

    [Fact]
    public void GivenTheCatalog_WhenCheckingScopes_ThenTheTableNamesOnlyCatalogActions()
    {
        var ids = ActionCatalog.All.Select(definition => definition.Id).ToHashSet();

        Assert.All(ActionCatalog.ScopedIds, id => Assert.Contains(id, ids));
    }

    [Fact]
    public void GivenTheCatalog_WhenCheckingGroups_ThenEveryNonModalActionHasAGroup()
    {
        var listed = ActionCatalog.All.Where(definition => !ActionCatalog.IsModalOnly(definition.Id));

        Assert.All(listed, definition => Assert.NotNull(ActionCatalog.GroupOf(definition.Id)));
    }

    [Fact]
    public void GivenEverydayLevel_WhenShowingAllActions_ThenAdvancedOnesAreLeftOutAndCounted()
    {
        var lines = HelpPages.Lines(HelpAt(1), _keymap);

        Assert.DoesNotContain(lines, line => line.Contains(ActionCatalog.FindTitle(ActionIds.IssueDelete)!));
        Assert.Contains(lines, line => line.Contains(ActionCatalog.FindTitle(ActionIds.IssueTransition)!));
        Assert.Contains(lines, line => line.Contains("advanced commands hidden; Space * shows them"));
    }

    [Fact]
    public void GivenAdvancedLevel_WhenShowingAllActions_ThenNothingIsHiddenAndNoNoteShows()
    {
        var lines = HelpPages.Lines(Advanced(HelpAt(1)), _keymap);

        Assert.Contains(lines, line => line.Contains(ActionCatalog.FindTitle(ActionIds.IssueDelete)!));
        Assert.DoesNotContain(lines, line => line.Contains("hidden"));
    }

    [Fact]
    public void GivenTheCommandTable_WhenShowingTheCommandsPage_ThenEveryCommandAppears()
    {
        var lines = HelpPages.Lines(HelpAt(2), _keymap);

        foreach (var command in PaletteCommands.All)
        {
            Assert.Contains($"  :{command.Usage}", lines);
        }
    }

    [Theory]
    [InlineData("QUIT")]
    [InlineData("app.quit")]
    [InlineData("ctrl-c")]
    public void GivenAFilter_WhenShowingAPage_ThenOnlyRowsMatchingKeyTitleOrIdRemain(string filter)
    {
        var lines = HelpPages.Lines(HelpAt(1) with { HelpFilter = filter }, _keymap);
        var rows = lines.Where(line => line.StartsWith("  ", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(rows);
        Assert.All(rows, line => Assert.Contains("Quit", line));
    }

    [Fact]
    public void GivenAFilterWithoutMatch_WhenShowingAPage_ThenThePageSaysSo()
    {
        var lines = HelpPages.Lines(HelpAt(1) with { HelpFilter = "zzzz" }, _keymap);

        Assert.Equal(["  Nothing matches /zzzz"], lines);
    }

    [Fact]
    public void GivenNoKeymapFile_WhenListingThePages_ThenThereIsNoCustomPage()
    {
        Assert.DoesNotContain(HelpPages.CustomName, HelpPages.Names(Help(AppState.Initial), _keymap));
    }

    [Fact]
    public void GivenARebindAndAnUnbind_WhenShowingTheCustomPage_ThenBothAreListedAndSkippedRowsAreNot()
    {
        const string json = """
            {
              "bindings": [
                { "mode": "normal", "keys": "Ctrl-q", "action": "app.quit" },
                { "mode": "normal", "keys": "Alt-j", "action": "no.such-action" }
              ],
              "unbind": [ { "mode": "normal", "keys": "q" } ]
            }
            """;
        var result = UserKeymap.Merge(_keymap.Bindings, json);
        var custom = new KeymapTable(result.Bindings);

        custom.Replace(result.Bindings, result.Custom);

        var names = HelpPages.Names(Help(AppState.Initial), custom);
        var lines = HelpPages.Lines(HelpAt(names.Count - 1), custom);

        Assert.Equal(HelpPages.CustomName, names[^1]);
        Assert.Contains(lines, line => line.Contains("Ctrl-q") && line.Contains("app.quit") && line.Contains("Quit"));
        Assert.Contains("  Unbound: q", lines);
        Assert.DoesNotContain(lines, line => line.Contains("no.such-action"));
    }

    private static AppState Help(AppState state)
    {
        return state with { Mode = InputMode.Help };
    }

    private static AppState HelpAt(int page)
    {
        return Help(AppState.Initial) with { HelpPage = page };
    }

    private static AppState Advanced(AppState state)
    {
        return state with { Settings = AppSettings.Default with { CommandLevel = CommandLevel.Advanced } };
    }

    private AppState Opened(ViewSourceKind source, ViewLayout layout)
    {
        var reducer = new AppReducer(_keymap);
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
        var opened = reducer.Reduce(start, new ViewOpenRequested(new ViewDefinition(source, "1", layout, "T")));
        var tab = opened.State.Session.Tabs[0];
        var sized = reducer.Reduce(opened.State, new ViewportChanged(20));
        var issue = new IssueSummary(
            "1",
            "A-1",
            "Summary",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
        var page = new IssuePage([issue], null, null);

        return reducer.Reduce(sized.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
    }
}
