using Geeratya.Core.CommandPalette;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Settings;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.CommandPalette;

[Trait("Os", "Independent")]
public class PaletteRowsTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly string[] _hidden = [ActionIds.PaletteOpen, ActionIds.Cancel];

    private readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Fact]
    public void GivenNoFilter_WhenListing_ThenEveryNonModalActionHasARowWithItsKeys()
    {
        var rows = PaletteRows.For(Palette(string.Empty, CommandLevel.Advanced), _keymap)
            .Where(row => row.Kind == PaletteRowKind.Action)
            .ToList();
        var expected = ActionCatalog.All
            .Where(definition => !ActionCatalog.IsModalOnly(definition.Id) && !_hidden.Contains(definition.Id))
            .Select(definition => definition.Id);

        Assert.Equal(expected.Order(), rows.Select(row => row.Id).Order());
        Assert.Equal("Space i s", rows.Single(row => row.Id == ActionIds.IssueTransition).Keys);
        Assert.Equal("j, Down", rows.Single(row => row.Id == ActionIds.NavigateDown).Keys);
    }

    [Fact]
    public void GivenEverydayLevelAndNoFilter_WhenListing_ThenAdvancedActionsAreLeftOutAndCounted()
    {
        var rows = PaletteRows.For(Palette(string.Empty), _keymap);
        var advanced = ActionCatalog.All.Where(definition => definition.Level == CommandLevel.Advanced).ToList();

        Assert.NotEmpty(advanced);
        Assert.All(advanced, definition => Assert.DoesNotContain(rows, row => row.Id == definition.Id));
        Assert.Contains(rows, row => row.Id == ActionIds.IssueTransition);

        var note = rows[^1];

        Assert.Equal(PaletteRowKind.Note, note.Kind);
        Assert.False(note.IsSelectable);
        Assert.Equal($"{advanced.Count} advanced commands hidden; type to find them", note.Title);
    }

    [Fact]
    public void GivenEverydayLevelAndAFilter_WhenListing_ThenAdvancedActionsStillMatchSoTheyRunFromThePalette()
    {
        var rows = PaletteRows.For(Palette("delete the issue"), _keymap);

        Assert.Contains(rows, row => row.Id == ActionIds.IssueDelete);
        Assert.DoesNotContain(rows, row => row.Kind == PaletteRowKind.Note);
    }

    [Fact]
    public void GivenAdvancedLevelAndNoFilter_WhenListing_ThenAdvancedActionsShowAndNoNoteEnds()
    {
        var rows = PaletteRows.For(Palette(string.Empty, CommandLevel.Advanced), _keymap);

        Assert.Contains(rows, row => row.Id == ActionIds.IssueDelete);
        Assert.DoesNotContain(rows, row => row.Kind == PaletteRowKind.Note);
    }

    [Fact]
    public void GivenAnyLevel_WhenListing_ThenTheLevelNeverMakesAnActionUnavailable()
    {
        var rows = PaletteRows.For(Palette("rank to the top"), _keymap);

        Assert.True(rows.Single(row => row.Id == ActionIds.RankTop).Enabled);
    }

    [Fact]
    public void GivenNoFilter_WhenListing_ThenModalOnlyActionsAreNotListed()
    {
        var ids = PaletteRows.For(Palette(string.Empty), _keymap).Select(row => row.Id).ToList();

        Assert.All(ActionCatalog.ModalOnly, id => Assert.DoesNotContain(id, ids));
        Assert.DoesNotContain(ActionIds.PaletteOpen, ids);
        Assert.DoesNotContain(ActionIds.Cancel, ids);
    }

    [Fact]
    public void GivenEveryRow_WhenListing_ThenItSitsUnderASubheaderOfItsGroupInTheFixedOrder()
    {
        var rows = PaletteRows.For(Palette(string.Empty), _keymap);
        var headers = rows.Where(row => row.Kind == PaletteRowKind.Header).Select(row => row.Title).ToList();
        var group = string.Empty;

        foreach (var row in rows)
        {
            group = row.Kind == PaletteRowKind.Header ? row.Title : group;

            if (row.Kind == PaletteRowKind.Action)
            {
                Assert.Equal(ActionCatalog.GroupOf(row.Id), group);
            }
        }

        Assert.Equal(ActionCatalog.Groups, headers);
        Assert.Equal(PaletteRowKind.Header, rows[0].Kind);
    }

    [Fact]
    public void GivenAFilter_WhenListing_ThenGroupsWithoutAMatchDisappearAndTheBestGroupComesFirst()
    {
        var rows = PaletteRows.For(Palette("rank"), _keymap);
        var headers = rows.Where(row => row.Kind == PaletteRowKind.Header).Select(row => row.Title).ToList();

        Assert.Equal("Rank", headers[0]);
        Assert.DoesNotContain("Tabs", headers);
        Assert.DoesNotContain("Comment", headers);
    }

    [Fact]
    public void GivenAFilter_WhenListing_ThenATitleMatchOutranksAnIdOnlyMatch()
    {
        var rows = PaletteRows.For(Palette("jump"), _keymap).Where(row => row.IsSelectable).ToList();

        Assert.Equal(ActionIds.IssueJump, rows[0].Id);
    }

    [Fact]
    public void GivenAFilterMatchingNothing_WhenListing_ThenTheListIsEmpty()
    {
        Assert.Empty(PaletteRows.For(Palette("zzqx"), _keymap));
    }

    [Fact]
    public void GivenAUserRebind_WhenListing_ThenTheRowShowsTheNewKeyAndTheOldOneIsGone()
    {
        var bindings = _keymap.Bindings.Where(binding => binding.Action != ActionIds.TabClose).ToList();

        bindings.Add(new KeyBinding(InputMode.Normal, "Alt-w", ActionIds.TabClose));

        var rebound = new KeymapTable(bindings);
        var row = PaletteRows.For(Palette("close"), rebound).Single(candidate => candidate.Id == ActionIds.TabClose);

        Assert.Equal("Alt-w", row.Keys);
    }

    [Fact]
    public void GivenAnActionWithoutAKey_WhenListing_ThenItIsStillListedWithNoKeys()
    {
        var bindings = _keymap.Bindings.Where(binding => binding.Action != ActionIds.TabClose).ToList();
        var unbound = new KeymapTable(bindings);

        var row = PaletteRows.For(Palette("close"), unbound).Single(candidate => candidate.Id == ActionIds.TabClose);

        Assert.Equal(string.Empty, row.Keys);
        Assert.True(row.Enabled);
    }

    [Fact]
    public void GivenPaletteOnlyCommands_WhenListing_ThenTheyRunAsTheirCommandLine()
    {
        var labels = PaletteRows.For(Palette("Edit the labels"), _keymap).Single(row => row.Kind == PaletteRowKind.Command);

        Assert.Equal(PaletteCommands.Labels, labels.Id);
        Assert.Equal(string.Empty, labels.Keys);
    }

    [Fact]
    public void GivenATabOpen_WhenListing_ThenTheActionsOfTheFocusedViewComeFirstInTheirGroup()
    {
        var unfocused = PaletteRows.For(Palette(string.Empty), _keymap).Where(row => row.Kind == PaletteRowKind.Action);
        var focused = PaletteRows.For(WithTab(Palette(string.Empty)), _keymap).Where(row => row.Kind == PaletteRowKind.Action);
        var tabs = focused.Where(row => ActionCatalog.GroupOf(row.Id) == "Tabs").Select(row => row.Id).ToList();

        Assert.Equal(ActionIds.TabClose, tabs[0]);
        Assert.NotEqual(ActionIds.TabClose, unfocused.First(row => ActionCatalog.GroupOf(row.Id) == "Tabs").Id);
    }

    [Theory]
    [InlineData("jql")]
    [InlineData("jql project = A")]
    [InlineData("tab next")]
    [InlineData("profile list")]
    [InlineData("set hints.statusBar off")]
    public void GivenACommandNameAndASpace_WhenListing_ThenTheListIsThatCommandsUsage(string text)
    {
        var rows = PaletteRows.For(Palette(text), _keymap);
        var name = text.Split(' ')[0];

        Assert.All(rows, row => Assert.Equal(PaletteRowKind.Usage, row.Kind));
        Assert.Equal(PaletteCommands.UsageOf(name), rows[0].Title);
        Assert.True(PaletteRows.IsCommandLine(text));
        Assert.Empty(PaletteRows.Selectable(Palette(text), _keymap));
    }

    [Theory]
    [InlineData("quit", true)]
    [InlineData("q", true)]
    [InlineData("h", true)]
    [InlineData("create subtask", true)]
    [InlineData("layout list", true)]
    [InlineData("browse GCM-1", true)]
    [InlineData("cre", false)]
    [InlineData("rank", false)]
    [InlineData("", false)]
    public void GivenText_WhenAskingIfItIsACommandLine_ThenOnlyACommandWordCounts(string text, bool expected)
    {
        Assert.Equal(expected, PaletteRows.IsCommandLine(text));
    }

    [Fact]
    public void GivenAnEmojiFilter_WhenListing_ThenItDoesNotThrow()
    {
        Assert.Empty(PaletteRows.For(Palette("😀"), _keymap));
    }

    [Fact]
    public void GivenThemeNames_WhenListing_ThenEachThemeHasARowInGeneralThatRunsItsCommandLine()
    {
        var state = Palette("Theme:") with { ThemeNames = ["dracula", "nord"] };

        var rows = PaletteRows.For(state, _keymap);
        var themes = rows.Where(row => row.Kind == PaletteRowKind.Command).ToList();

        Assert.Equal(["Theme: dracula", "Theme: nord"], themes.Select(row => row.Title));
        Assert.Equal(["theme dracula", "theme nord"], themes.Select(row => row.Id));
        Assert.Equal("General", rows[0].Title);
    }

    [Fact]
    public void GivenNoThemeNames_WhenListing_ThenNoThemeRowShows()
    {
        var rows = PaletteRows.For(Palette("Theme:"), _keymap);

        Assert.DoesNotContain(rows, row => row.Id.StartsWith("theme ", StringComparison.Ordinal));
    }

    private static AppState Palette(string text, CommandLevel level = CommandLevel.Everyday)
    {
        return AppState.Initial with
        {
            Mode = InputMode.Command,
            PaletteText = text,
            Settings = AppSettings.Default with { CommandLevel = level },
        };
    }

    private static AppState WithTab(AppState state)
    {
        var reducer = new AppReducer(DefaultKeymap.Load());
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = state with { Profiles = profiles, Session = SiteSession.Empty(_work) };
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "View");
        var opened = reducer.Reduce(start, new ViewOpenRequested(definition)).State;
        var tab = opened.Session.Tabs.Single();
        var rows = new IssuePage([], null, 0);

        return reducer.Reduce(opened, new ViewPageLoaded(tab.Id, tab.RequestId, rows, _now)).State;
    }
}
