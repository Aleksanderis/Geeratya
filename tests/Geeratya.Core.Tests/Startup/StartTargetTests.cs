using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Detail;
using Geeratya.Core.Settings;
using Geeratya.Core.Sources;
using Geeratya.Core.Startup;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Filters.Models;

namespace Geeratya.Core.Tests.Startup;

[Trait("Os", "Independent")]
public class StartTargetTests
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly Profile _home = new("home", "https://home.atlassian.net", "me@home.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenAnIssueTarget_WhenNothingIsRemembered_ThenTheIssueOpensInATabAndNoPickerOpens()
    {
        var started = Begin(Target(StartTargetKind.Issue, "GEN-1"));
        var result = _reducer.Reduce(started, new TabsLoaded(_work, RememberedTabs.Empty));

        var tab = Assert.Single(result.State.Session.Tabs);
        Assert.Equal("key = GEN-1", tab.Definition.SourceValue);
        Assert.Contains(result.Effects, effect => effect is LoadIssueDetailEffect { Key: "GEN-1" });
        Assert.Null(result.State.Picker);
        Assert.Null(result.State.PendingStart);
    }

    [Fact]
    public void GivenAnIssueTarget_WhenTabsAreRemembered_ThenTheTargetOpensOnTopOfThem()
    {
        var remembered = new RememberedTabs([new RememberedTab("Jql", "project = A", "List", "A")]);
        var result = _reducer.Reduce(Begin(Target(StartTargetKind.Issue, "GEN-1")), new TabsLoaded(_work, remembered));

        Assert.Equal(2, result.State.Session.Tabs.Count);
        Assert.Equal("project = A", result.State.Session.Tabs[0].Definition.SourceValue);
        Assert.Equal("key = GEN-1", result.State.Session.Tabs[1].Definition.SourceValue);
        Assert.Equal(result.State.Session.Tabs[1].Id, result.State.Session.Active);
    }

    [Fact]
    public void GivenAJqlTarget_WhenTheTabsAreRead_ThenTheQueryOpensAsATab()
    {
        var result = _reducer.Reduce(
            Begin(Target(StartTargetKind.Jql, "assignee = currentUser()")),
            new TabsLoaded(_work, RememberedTabs.Empty));

        var tab = Assert.Single(result.State.Session.Tabs);
        Assert.Equal(ViewSourceKind.Jql, tab.Definition.Source);
        Assert.Equal("assignee = currentUser()", tab.Definition.SourceValue);
    }

    [Fact]
    public void GivenATargetAndTheTabsNotYetRead_WhenStartupBegins_ThenNothingOpensBeforeTheFileArrives()
    {
        var started = Begin(Target(StartTargetKind.Jql, "project = A"));

        Assert.Empty(started.Session.Tabs);
        Assert.NotNull(started.PendingStart);
    }

    [Fact]
    public void GivenABoardTarget_WhenTheBoardListArrives_ThenTheBoardOpensAsColumns()
    {
        var loaded = _reducer.Reduce(Begin(Target(StartTargetKind.Board, "GCM")), new TabsLoaded(_work, RememberedTabs.Empty));
        var boards = new BoardSummary[] { new(4, "GCM board", BoardType.Kanban, "GCM"), new(3, "GTM board", BoardType.Simple, "GTM") };
        var result = _reducer.Reduce(loaded.State, new BoardsLoaded(boards));

        var tab = Assert.Single(result.State.Session.Tabs);
        Assert.Equal(ViewSourceKind.Board, tab.Definition.Source);
        Assert.Equal("4", tab.Definition.SourceValue);
    }

    [Fact]
    public void GivenABoardTargetThatMatchesNothing_WhenTheBoardListArrives_ThenThePickerStaysOpenWithANotice()
    {
        var loaded = _reducer.Reduce(Begin(Target(StartTargetKind.Board, "Nope")), new TabsLoaded(_work, RememberedTabs.Empty));
        var boards = new BoardSummary[] { new(4, "GCM board", BoardType.Kanban, "GCM") };
        var result = _reducer.Reduce(loaded.State, new BoardsLoaded(boards));

        Assert.Empty(result.State.Session.Tabs);
        Assert.Contains("Nope", result.State.Picker!.Notice);
    }

    [Fact]
    public void GivenATargetAndRestoringOff_WhenStartupBegins_ThenTheTargetOpensAlone()
    {
        var state = Target(StartTargetKind.Jql, "project = A") with
        {
            Settings = AppSettings.Default with { RestoreTabs = false },
        };

        var result = _reducer.Reduce(state, new StartupRequested());

        var tab = Assert.Single(result.State.Session.Tabs);
        Assert.Equal("project = A", tab.Definition.SourceValue);
        Assert.DoesNotContain(result.Effects, effect => effect is LoadTabsEffect);
        Assert.Null(result.State.Picker);
    }

    [Fact]
    public void GivenANamedProfile_WhenOptionsApply_ThenItBecomesActiveForTheRunAndKeepsTheTarget()
    {
        var state = StartupReducer.WithOptions(
            Initial(),
            new StartOptions("home", new StartTarget(StartTargetKind.Issue, "GEN-1")));

        Assert.Equal(_home, state.Profiles.Active);
        Assert.NotNull(state.PendingStart);
    }

    [Fact]
    public void GivenAnUnknownProfile_WhenOptionsApply_ThenTheTargetIsDroppedWithAWarning()
    {
        var state = StartupReducer.WithOptions(
            Initial(),
            new StartOptions("nope", new StartTarget(StartTargetKind.Issue, "GEN-1")));

        Assert.Null(state.PendingStart);
        Assert.Equal(_work, state.Profiles.Active);
        Assert.Contains("nope", state.Message);
    }

    [Fact]
    public void GivenTabsRememberedAndATarget_WhenTheyArrive_ThenNothingIsSavedAtOnce()
    {
        var remembered = new RememberedTabs([new RememberedTab("Jql", "project = A", "List", "A")]);
        var result = _reducer.Reduce(Begin(Target(StartTargetKind.Issue, "GEN-1")), new TabsLoaded(_work, remembered));

        Assert.DoesNotContain(result.Effects, effect => effect is SaveTabsEffect);
    }

    [Fact]
    public void GivenATargetPendingWhileTabsLoad_WhenTheProfileSwitches_ThenTheTargetIsDropped()
    {
        var started = Begin(Target(StartTargetKind.Jql, "project = A"));
        var switched = StartupReducer.Begin(new ReduceResult(started with { Session = started.Session with { Startup = StartupPhase.Done } }, []));
        var result = _reducer.Reduce(switched.State, new TabsLoaded(_work, RememberedTabs.Empty));

        Assert.Empty(result.State.Session.Tabs);
    }

    [Fact]
    public void GivenABacklogTarget_WhenTheBoardListArrives_ThenTheBacklogOpens()
    {
        var loaded = _reducer.Reduce(Begin(Target(StartTargetKind.Backlog, "GCM")), new TabsLoaded(_work, RememberedTabs.Empty));
        var boards = new BoardSummary[] { new(4, "GCM board", BoardType.Kanban, "GCM"), new(3, "GTM board", BoardType.Simple, "GTM") };
        var result = _reducer.Reduce(loaded.State, new BoardsLoaded(boards));

        var tab = Assert.Single(result.State.Session.Tabs);
        Assert.Equal(ViewSourceKind.Backlog, tab.Definition.Source);
    }

    [Fact]
    public void GivenAFilterTarget_WhenTheFilterListArrives_ThenTheFilterOpensAndAMissOnesLeavesANotice()
    {
        var filters = new SavedFilter[] { new("10", "My open bugs", "type = Bug", null, false, null) };
        var hit = _reducer.Reduce(Begin(Target(StartTargetKind.Filter, "My open")), new TabsLoaded(_work, RememberedTabs.Empty));
        var opened = _reducer.Reduce(hit.State, new FiltersLoaded(filters));
        var miss = _reducer.Reduce(Begin(Target(StartTargetKind.Filter, "zzz")), new TabsLoaded(_work, RememberedTabs.Empty));
        var missed = _reducer.Reduce(miss.State, new FiltersLoaded(filters));

        Assert.Equal(ViewSourceKind.SavedFilter, Assert.Single(opened.State.Session.Tabs).Definition.Source);
        Assert.Empty(missed.State.Session.Tabs);
        Assert.Contains("zzz", missed.State.Picker!.Notice);
    }

    private static AppState Initial()
    {
        var catalog = ProfileCatalog.Empty.With(_work).With(_home).Activate("work");

        return AppState.Initial with { Profiles = catalog, Session = SiteSession.Empty(catalog.Active) };
    }

    private static AppState Target(StartTargetKind kind, string value)
    {
        return StartupReducer.WithOptions(Initial(), new StartOptions(null, new StartTarget(kind, value)));
    }

    private AppState Begin(AppState state)
    {
        return _reducer.Reduce(state, new StartupRequested()).State;
    }
}
