using Geeratya.Core.Boards;
using Geeratya.Core.Cache;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Settings;
using Geeratya.Core.Sources;
using Geeratya.Core.Startup;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Users;

namespace Geeratya.Core.Tests.Startup;

[Trait("Os", "Independent")]
public class StartupReducerTests
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly Profile _home = new("home", "https://home.atlassian.net", "me@home.test");
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    private static readonly BoardSummary[] _oneBoard = [new(4, "GCM board", BoardType.Kanban, "GCM")];

    private static readonly BoardSummary[] _boards =
    [
        new(4, "GCM board", BoardType.Kanban, "GCM"),
        new(3, "GTM board", BoardType.Simple, "GTM"),
    ];

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenAnActiveProfile_WhenStartupIsRequested_ThenTheRememberedTabsAreRead()
    {
        var result = _reducer.Reduce(Initial(), new StartupRequested());

        Assert.Equal(StartupPhase.Loading, result.State.Session.Startup);
        Assert.Equal(_work, Assert.Single(result.Effects.OfType<LoadTabsEffect>()).Profile);
    }

    [Fact]
    public void GivenNoProfile_WhenStartupIsRequested_ThenNothingIsRead()
    {
        var result = _reducer.Reduce(AppState.Initial, new StartupRequested());

        Assert.Empty(result.Effects);
        Assert.Equal(StartupPhase.Idle, result.State.Session.Startup);
    }

    [Fact]
    public void GivenNothingRemembered_WhenTheConnectionWorks_ThenTheBoardPickerOpens()
    {
        var loaded = Connected(Begin(Initial()));
        var result = _reducer.Reduce(loaded, new TabsLoaded(_work, RememberedTabs.Empty));

        Assert.Equal(InputMode.Insert, result.State.Mode);
        Assert.Equal(PickerScope.Boards, result.State.Picker!.Scope);
        Assert.True(Assert.Single(result.Effects.OfType<LoadSourcesEffect>()).Boards);
        Assert.Empty(result.State.Session.Tabs);
    }

    [Fact]
    public void GivenNothingRememberedAndNoReplyYet_WhenTheConnectionIsChecked_ThenTheBoardPickerOpensThen()
    {
        var waiting = _reducer.Reduce(Begin(Initial()), new TabsLoaded(_work, RememberedTabs.Empty));

        Assert.Null(waiting.State.Picker);
        Assert.Equal(StartupPhase.AwaitingConnection, waiting.State.Session.Startup);

        var result = _reducer.Reduce(waiting.State, new ConnectionChecked(_work, User()));

        Assert.Equal(PickerScope.Boards, result.State.Picker!.Scope);
        Assert.Single(result.Effects.OfType<LoadSourcesEffect>());
    }

    [Fact]
    public void GivenSeveralBoards_WhenTheyArrive_ThenThePickerStaysOpenToChoose()
    {
        var picker = _reducer.Reduce(Connected(Begin(Initial())), new TabsLoaded(_work, RememberedTabs.Empty));
        var result = _reducer.Reduce(picker.State, new BoardsLoaded(_boards));

        Assert.NotNull(result.State.Picker);
        Assert.Empty(result.State.Session.Tabs);
    }

    [Fact]
    public void GivenExactlyOneBoard_WhenItArrives_ThenItOpensWithoutAPicker()
    {
        var picker = _reducer.Reduce(Connected(Begin(Initial())), new TabsLoaded(_work, RememberedTabs.Empty));
        var result = _reducer.Reduce(picker.State, new BoardsLoaded(_oneBoard));

        var tab = Assert.Single(result.State.Session.Tabs);
        Assert.Equal(ViewSourceKind.Board, tab.Definition.Source);
        Assert.Null(result.State.Picker);
    }

    [Fact]
    public void GivenRememberedTabs_WhenTheyArrive_ThenTheyReopenAndOnlyTheActiveOneLoads()
    {
        var remembered = new RememberedTabs(
            [
                new RememberedTab("Jql", "project = A", "List", "A"),
                new RememberedTab("Board", "4", "Columns", "GCM board"),
                new RememberedTab("Jql", "project = B", "List", "B"),
            ],
            1);

        var result = _reducer.Reduce(Begin(Initial()), new TabsLoaded(_work, remembered));
        var tabs = result.State.Session.Tabs;

        Assert.Equal(["A", "GCM board", "B"], tabs.Select(tab => tab.Definition.Title));
        Assert.Equal(tabs[1].Id, result.State.Session.Active);
        Assert.Equal(ViewLayout.Columns, tabs[1].Definition.Layout);
        Assert.Equal([true, false, true], tabs.Select(tab => tab.Cold));
        Assert.Equal(tabs[1].Id, Assert.Single(result.Effects.OfType<LoadViewPageEffect>()).Tab);
        Assert.Null(result.State.Picker);
    }

    [Fact]
    public void GivenARestoredColdTab_WhenItIsShown_ThenItLoads()
    {
        var restored = _reducer.Reduce(Begin(Initial()), new TabsLoaded(_work, Two()));
        var result = _reducer.Reduce(restored.State, new TabActivated(restored.State.Session.Tabs[0].Id));

        Assert.Single(result.Effects.OfType<LoadViewPageEffect>());
    }

    [Fact]
    public void GivenAFileWithUnknownEntries_WhenTheyArrive_ThenTheyAreSkipped()
    {
        var remembered = new RememberedTabs(
            [
                new RememberedTab("Hologram", "x", "List", "Future"),
                new RememberedTab("Jql", "project = A", "Cube", "Future too"),
                new RememberedTab("Jql", "project = A", "List", "A"),
            ],
            2);

        var result = _reducer.Reduce(Begin(Initial()), new TabsLoaded(_work, remembered));

        var tab = Assert.Single(result.State.Session.Tabs);
        Assert.Equal("A", tab.Definition.Title);
        Assert.Equal(tab.Id, result.State.Session.Active);
    }

    [Fact]
    public void GivenARestoredBoardWithAQuickFilter_WhenItLoads_ThenTheFirstPageWaitsForTheFilterClause()
    {
        var remembered = new RememberedTabs(
            [new RememberedTab("Board", "4", "Columns", "GCM board", [7L], nameof(SwimlaneKind.Assignee), "alice")]);

        var restored = _reducer.Reduce(Begin(Initial()), new TabsLoaded(_work, remembered));
        var tab = Assert.Single(restored.State.Session.Tabs);

        Assert.Empty(restored.Effects.OfType<LoadViewPageEffect>());
        Assert.Equal(tab.Id, Assert.Single(restored.Effects.OfType<LoadQuickFiltersEffect>()).Tab);
        Assert.Equal(SwimlaneKind.Assignee, tab.Swimlane);
        Assert.Equal("alice", tab.Lane);

        var filters = new QuickFiltersLoaded(tab.Id, [new QuickFilter(7, "Mine", "assignee = currentUser()", 1)], false);
        var loading = _reducer.Reduce(restored.State, filters);
        var page = Assert.Single(loading.Effects.OfType<LoadViewPageEffect>());

        Assert.Equal("(assignee = currentUser())", page.ExtraJql);
        Assert.Equal(loading.State.Session.Tabs[0].RequestId, page.RequestId);
        Assert.NotEqual(0, page.RequestId);
        Assert.False(loading.State.Session.Tabs[0].AwaitingFilters);
    }

    [Fact]
    public void GivenARestoredQuickFilterLookupThatFails_WhenItArrives_ThenThePageLoadsWithoutTheFilterClause()
    {
        var remembered = new RememberedTabs([new RememberedTab("Board", "4", "Columns", "GCM board", [7L])]);
        var restored = _reducer.Reduce(Begin(Initial()), new TabsLoaded(_work, remembered));
        var tab = Assert.Single(restored.State.Session.Tabs);

        var failed = _reducer.Reduce(restored.State, new QuickFiltersLoaded(tab.Id, [], false, "Quick filters failed"));

        Assert.Null(Assert.Single(failed.Effects.OfType<LoadViewPageEffect>()).ExtraJql);
    }

    [Fact]
    public void GivenTheOnlyRestoredBoardIsGone_WhenItsLoadFails_ThenTheTabIsDroppedAndThePickerOpens()
    {
        var remembered = new RememberedTabs([new RememberedTab("Board", "99", "Columns", "Old board")]);
        var restored = _reducer.Reduce(Connected(Begin(Initial())), new TabsLoaded(_work, remembered));
        var tab = Assert.Single(restored.State.Session.Tabs);

        var result = _reducer.Reduce(restored.State, Failure(tab, JiraErrorKind.NotFound));

        Assert.Empty(result.State.Session.Tabs);
        Assert.Equal(PickerScope.Boards, result.State.Picker!.Scope);
        Assert.Contains("Old board", result.State.Message);
        Assert.Equal(StatusSeverity.Warning, result.State.Status!.Severity);
    }

    [Fact]
    public void GivenARestoredBoardServedFromTheCache_WhenTheNetworkSaysNotFound_ThenTheTabIsDropped()
    {
        var remembered = new RememberedTabs([new RememberedTab("Board", "99", "Columns", "Old board")]);
        var restored = _reducer.Reduce(Connected(Begin(Initial())), new TabsLoaded(_work, remembered));
        var tab = Assert.Single(restored.State.Session.Tabs);
        var cached = _reducer.Reduce(restored.State, new ViewCacheLoaded(tab.Id, tab.RequestId, new CachedView([CachedIssue()], null, _now)));

        Assert.NotNull(Assert.Single(cached.State.Session.Tabs).LoadedAt);

        var result = _reducer.Reduce(cached.State, Failure(tab, JiraErrorKind.NotFound));

        Assert.Empty(result.State.Session.Tabs);
        Assert.Equal(PickerScope.Boards, result.State.Picker!.Scope);
    }

    [Fact]
    public void GivenARestoredQuickFilterLookupThatFails_WhenItArrives_ThenTheRememberedFilterIdsStayAndNothingDropsThem()
    {
        var remembered = new RememberedTabs([new RememberedTab("Board", "4", "Columns", "GCM board", [7L])]);
        var restored = _reducer.Reduce(Begin(Initial()), new TabsLoaded(_work, remembered));
        var tab = Assert.Single(restored.State.Session.Tabs);

        var failed = _reducer.Reduce(restored.State, new QuickFiltersLoaded(tab.Id, [], false, "Quick filters failed"));

        Assert.Equal([7L], failed.State.Session.Tabs[0].ActiveQuickFilters);
        Assert.Empty(failed.Effects.OfType<SaveTabsEffect>());
    }

    [Fact]
    public void GivenATabOpenedWhileTheFileWasRead_WhenTheFileArrives_ThenTheOpenTabsAreSaved()
    {
        var opened = _reducer.Reduce(Begin(Initial()), new ViewOpenRequested(Jql()));
        var result = _reducer.Reduce(opened.State, new TabsLoaded(_work, Two()));

        Assert.Single(result.State.Session.Tabs);
        Assert.Equal("A", Assert.Single(Assert.Single(result.Effects.OfType<SaveTabsEffect>()).Tabs.Items).Title);
    }

    [Fact]
    public void GivenOneOfTwoRestoredTabsIsGone_WhenItsLoadFails_ThenOnlyItIsDropped()
    {
        var restored = _reducer.Reduce(Begin(Initial()), new TabsLoaded(_work, Two()));
        var active = restored.State.Session.Tabs[1];

        var result = _reducer.Reduce(restored.State, Failure(active, JiraErrorKind.NotFound));

        Assert.Equal(["A"], result.State.Session.Tabs.Select(tab => tab.Definition.Title));
        Assert.Null(result.State.Picker);
    }

    [Fact]
    public void GivenARestoredTab_WhenItsLoadFailsForAnotherReason_ThenItStaysWithItsError()
    {
        var restored = _reducer.Reduce(Begin(Initial()), new TabsLoaded(_work, Two()));
        var active = restored.State.Session.Tabs[1];

        var result = _reducer.Reduce(restored.State, Failure(active, JiraErrorKind.Network));

        Assert.Equal(2, result.State.Session.Tabs.Count);
    }

    [Fact]
    public void GivenATabTheUserOpened_WhenItsLoadIsNotFound_ThenItStays()
    {
        var opened = _reducer.Reduce(Done(), new ViewOpenRequested(new ViewDefinition(ViewSourceKind.Board, "9", ViewLayout.Columns, "B")));
        var tab = Assert.Single(opened.State.Session.Tabs);

        var result = _reducer.Reduce(opened.State, Failure(tab, JiraErrorKind.NotFound));

        Assert.Single(result.State.Session.Tabs);
    }

    [Fact]
    public void GivenAReplyForAnotherProfile_WhenItArrives_ThenItIsIgnored()
    {
        var result = _reducer.Reduce(Begin(Initial()), new TabsLoaded(_home, Two()));

        Assert.Empty(result.State.Session.Tabs);
        Assert.Equal(StartupPhase.Loading, result.State.Session.Startup);
    }

    [Fact]
    public void GivenTwoProfiles_WhenSwitching_ThenTheNewProfilesTabsAreReadAndTheOldOnesDropped()
    {
        var catalog = ProfileCatalog.Empty.With(_work).With(_home);
        var restored = _reducer.Reduce(Begin(Initial()), new TabsLoaded(_work, Two()));
        var other = catalog with { ActiveName = _home.Name };

        var result = _reducer.Reduce(restored.State, new ProfilesChanged(other, "Switched."));

        Assert.Empty(result.State.Session.Tabs);
        Assert.Equal(_home, Assert.Single(result.Effects.OfType<LoadTabsEffect>()).Profile);
        Assert.Equal(StartupPhase.Loading, result.State.Session.Startup);
    }

    [Fact]
    public void GivenTheSettingOff_WhenStartupIsRequested_ThenNothingIsReadAndThePickerOpensOnConnection()
    {
        var off = Initial() with { Settings = AppSettings.Default with { RestoreTabs = false } };
        var begun = _reducer.Reduce(off, new StartupRequested());

        Assert.Empty(begun.Effects.OfType<LoadTabsEffect>());

        var result = _reducer.Reduce(begun.State, new ConnectionChecked(_work, User()));

        Assert.Equal(PickerScope.Boards, result.State.Picker!.Scope);
    }

    [Fact]
    public void GivenTheSettingOff_WhenATabChanges_ThenNothingIsSaved()
    {
        var off = Initial() with { Settings = AppSettings.Default with { RestoreTabs = false } };
        var begun = _reducer.Reduce(off, new StartupRequested());

        var result = _reducer.Reduce(begun.State, new ViewOpenRequested(Jql()));

        Assert.Empty(result.Effects.OfType<SaveTabsEffect>());
    }

    [Fact]
    public void GivenTheStartIsDone_WhenATabOpensAndItsLayoutChanges_ThenEachChangeIsSaved()
    {
        var opened = _reducer.Reduce(Done(), new ViewOpenRequested(Jql()));
        var save = Assert.Single(opened.Effects.OfType<SaveTabsEffect>());

        Assert.Equal(_work, save.Profile);
        Assert.Equal("project = A", Assert.Single(save.Tabs.Items).SourceValue);

        var tab = Assert.Single(opened.State.Session.Tabs);
        var changed = _reducer.Reduce(opened.State, new ViewLayoutChanged(tab.Id, ViewLayout.Columns));

        Assert.Equal("Columns", Assert.Single(Assert.Single(changed.Effects.OfType<SaveTabsEffect>()).Tabs.Items).Layout);
    }

    [Fact]
    public void GivenTheRememberedTabsAreStillBeingRead_WhenATabOpens_ThenTheFileIsNotOverwritten()
    {
        var result = _reducer.Reduce(Begin(Initial()), new ViewOpenRequested(Jql()));

        Assert.Empty(result.Effects.OfType<SaveTabsEffect>());
    }

    [Fact]
    public void GivenARowArrives_WhenNoDefinitionChanged_ThenNothingIsSaved()
    {
        var opened = _reducer.Reduce(Done(), new ViewOpenRequested(Jql()));
        var result = _reducer.Reduce(opened.State, new Tick(_now));

        Assert.Empty(result.Effects.OfType<SaveTabsEffect>());
    }

    [Fact]
    public void GivenTheLastTabCloses_WhenTheStartIsDone_ThenAnEmptyListIsSaved()
    {
        var opened = _reducer.Reduce(Done(), new ViewOpenRequested(Jql()));
        var closed = _reducer.Reduce(opened.State, new TabClosed(opened.State.Session.Tabs[0].Id));

        Assert.Empty(Assert.Single(closed.Effects.OfType<SaveTabsEffect>()).Tabs.Items);
    }

    [Fact]
    public void GivenTabsWithOptions_WhenRememberedAndRestored_ThenTheOptionsComeBack()
    {
        var remembered = new RememberedTabs(
            [new RememberedTab("Board", "4", "Columns", "GCM board", [7L, 8L], nameof(SwimlaneKind.Epic), "KEY-1")]);

        var restored = _reducer.Reduce(Begin(Initial()), new TabsLoaded(_work, remembered));
        var again = RememberedTabs.Of(restored.State.Session);

        Assert.True(remembered.SameAs(again));
    }

    private static AppState Initial()
    {
        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_work), Session = SiteSession.Empty(_work) };
    }

    private AppState Begin(AppState state)
    {
        return _reducer.Reduce(state, new StartupRequested()).State;
    }

    private AppState Connected(AppState state)
    {
        return _reducer.Reduce(state, new ConnectionChecked(_work, User())).State;
    }

    private AppState Done()
    {
        return _reducer.Reduce(Begin(Initial()), new TabsLoaded(_work, RememberedTabs.Empty)).State;
    }

    private static ViewLoadFailed Failure(TabState tab, JiraErrorKind kind)
    {
        return new ViewLoadFailed(tab.Id, tab.RequestId, new JiraError(kind, null, "failed"), _now);
    }

    private static ViewDefinition Jql()
    {
        return new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "A");
    }

    private static RememberedTabs Two()
    {
        return new RememberedTabs(
            [new RememberedTab("Jql", "project = A", "List", "A"), new RememberedTab("Board", "4", "Columns", "GCM board")],
            1);
    }

    private static IssueSummary CachedIssue()
    {
        var status = new IssueStatus("1", "To Do", "new");
        var type = new IssueTypeSummary("10", "Task", 0, false);

        return new IssueSummary("1", "A-1", "Issue 1", status, type, null, null, [], null, null, "A");
    }

    private static JiraUser User()
    {
        return new JiraUser("acc-1", "Test User", null, true, null, null);
    }
}
