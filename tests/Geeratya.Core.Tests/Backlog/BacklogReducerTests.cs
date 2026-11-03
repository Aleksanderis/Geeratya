using Geeratya.Core.Backlog;
using Geeratya.Core.Cache;
using Geeratya.Core.Keymap;
using Geeratya.Core.Menus;
using Geeratya.Core.Lists;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Tests.Backlog;

[Trait("Os", "Independent")]
public class BacklogReducerTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenARankedPage_WhenLoaded_ThenTheTabHoldsTheRowsInSectionOrder()
    {
        var state = Loaded(BacklogFixtures.RankedRows);

        Assert.Equal(["A-3", "A-4", "A-2", "A-6", "A-1", "A-5"], Tab(state).Rows.Select(row => row.Key));
        Assert.Equal("A-3", Tab(state).SelectedKey);
    }

    [Fact]
    public void GivenASelection_WhenAnotherPageRegroupsTheRows_ThenTheSelectionFollowsItsIssue()
    {
        var first = new[] { BacklogFixtures.Issue("A-1", null, 1), BacklogFixtures.Issue("A-2", BacklogFixtures.Future, 1) };
        var state = Loaded(first, next: new PageCursor("2"));
        state = _reducer.Reduce(state, new KeyPressed("G", _now)).State;
        var tab = Tab(state);
        Assert.Equal("A-1", tab.SelectedKey);

        var more = new[] { BacklogFixtures.Issue("A-3", BacklogFixtures.Active, 1) };
        var next = _reducer.Reduce(state, new NextPageRequested(tab.Id)).State;
        var loading = Tab(next);
        var appended = _reducer.Reduce(next, new ViewPageLoaded(tab.Id, loading.RequestId, new IssuePage(more, null, null), _now));

        var result = Tab(appended.State);
        Assert.Equal(["A-3", "A-2", "A-1"], result.Rows.Select(row => row.Key));
        Assert.Equal("A-1", result.SelectedKey);
        Assert.Equal(2, result.Selected);
    }

    [Fact]
    public void GivenSprintsLoaded_WhenTheRequestIsCurrent_ThenTheTabKeepsThemAndAStaleReplyIsIgnored()
    {
        var opened = Opened();
        var tab = Tab(opened);
        var sprints = new[] { BacklogFixtures.Active };

        var current = _reducer.Reduce(opened, new SprintsLoaded(tab.Id, tab.RequestId, sprints)).State;
        var stale = _reducer.Reduce(opened, new SprintsLoaded(tab.Id, tab.RequestId + 99, sprints)).State;

        Assert.Single(Tab(current).Sprints);
        Assert.Empty(Tab(stale).Sprints);
    }

    [Fact]
    public void GivenCachedRows_WhenShownWhileLoading_ThenTheyAreGroupedToo()
    {
        var opened = Opened();
        var tab = Tab(opened);
        var view = new CachedView(BacklogFixtures.RankedRows, null, _now);

        var state = _reducer.Reduce(opened, new ViewCacheLoaded(tab.Id, tab.RequestId, view)).State;

        Assert.Equal("A-3", Tab(state).Rows[0].Key);
        Assert.True(Tab(state).IsStale);
    }

    [Fact]
    public void GivenTheToggleKey_WhenPressed_ThenTheSectionOfTheCursorCollapsesAndExpands()
    {
        var state = Loaded(BacklogFixtures.RankedRows);

        var collapsed = Press(Press(state, "z"), "a");
        var expanded = Press(Press(collapsed, "z"), "a");

        Assert.Equal([BacklogSections.SprintKey(BacklogFixtures.ActiveId)], Tab(collapsed).CollapsedSections);
        Assert.Empty(Tab(expanded).CollapsedSections);
    }

    [Fact]
    public void GivenTheCursorOnACollapsedHeader_WhenPressingEnter_ThenTheSectionExpandsInsteadOfOpeningAnIssue()
    {
        var state = Press(Press(Loaded(BacklogFixtures.RankedRows), "z"), "a");

        var entered = _reducer.Reduce(state, new KeyPressed("Enter", _now));

        Assert.Empty(Tab(entered.State).CollapsedSections);
        Assert.Empty(Tab(entered.State).Detail);
        Assert.Empty(entered.Effects);
    }

    [Fact]
    public void GivenTheCursorOnARow_WhenPressingEnter_ThenTheIssueOpens()
    {
        var entered = _reducer.Reduce(Loaded(BacklogFixtures.RankedRows), new KeyPressed("Enter", _now));

        Assert.Single(Tab(entered.State).Detail);
    }

    [Fact]
    public void GivenVisualMode_WhenCollapsing_ThenItIsRefusedBecauseHiddenIssuesWouldJoinTheRange()
    {
        var visual = Press(Loaded(BacklogFixtures.RankedRows), "v");

        var refused = Press(Press(visual, "z"), "a");

        Assert.Empty(Tab(refused).CollapsedSections);
        Assert.Contains("Visual", refused.Message);
    }

    [Fact]
    public void GivenTheSectionKeys_WhenPressed_ThenTheCursorJumpsBetweenSections()
    {
        var state = Loaded(BacklogFixtures.RankedRows);

        var next = Press(Press(state, "]"), "]");
        var back = Press(Press(next, "["), "[");

        Assert.Equal("A-2", Tab(next).SelectedKey);
        Assert.Equal("A-3", Tab(back).SelectedKey);
    }

    [Fact]
    public void GivenMotionKeys_WhenPressedAcrossSections_ThenSelectionEndsOnTheNeighbourRow()
    {
        var state = Loaded(BacklogFixtures.RankedRows);
        state = Press(state, "j");
        state = Press(state, "j");

        Assert.Equal("A-2", Tab(state).SelectedKey);
    }

    [Fact]
    public void GivenAClickOnASectionHeaderLine_WhenPlacingTheCursor_ThenItGoesToTheSectionsFirstRow()
    {
        var state = WithSprints(Loaded(BacklogFixtures.RankedRows));

        var placed = ViewReducer.PlaceCursor(state, new PointerPlaced(1, ListLayoutRow(4), 80));

        Assert.Equal("A-2", Tab(placed!.State).SelectedKey);
    }

    [Fact]
    public void GivenAClickOnTheNoteOfAnEmptySection_WhenPlacingTheCursor_ThenNothingIsPlaced()
    {
        var state = WithSprints(Loaded(BacklogFixtures.RankedRows));

        Assert.Null(ViewReducer.PlaceCursor(state, new PointerPlaced(1, ListLayoutRow(8), 80)));
    }

    [Fact]
    public void GivenARowClick_WhenPlacingTheCursor_ThenTheRowOfThatLineIsSelected()
    {
        var state = WithSprints(Loaded(BacklogFixtures.RankedRows));

        var placed = ViewReducer.PlaceCursor(state, new PointerPlaced(1, ListLayoutRow(11), 80));

        Assert.Equal("A-5", Tab(placed!.State).SelectedKey);
    }

    [Fact]
    public void GivenTheContextMenuOfABacklogRow_WhenBuilt_ThenItOffersTheSectionToggle()
    {
        var menu = ContextMenus.For(Loaded(BacklogFixtures.RankedRows));

        Assert.Contains(menu!.Items, item => item.Action == ActionIds.SectionToggle);
    }

    [Fact]
    public void GivenAPlainList_WhenPressingTheToggleKey_ThenNothingChanges()
    {
        var plain = Loaded(BacklogFixtures.RankedRows, ViewSourceKind.Backlog);

        var after = Press(Press(plain, "z"), "a");

        Assert.Empty(Tab(after).CollapsedSections);
        Assert.Equal("A-1", Tab(after).Rows[0].Key);
    }

    [Theory]
    [InlineData(BoardType.Scrum, ViewSourceKind.ScrumBacklog)]
    [InlineData(BoardType.Kanban, ViewSourceKind.Backlog)]
    [InlineData(BoardType.Simple, ViewSourceKind.Backlog)]
    public void GivenABoardType_WhenOpeningItsBacklog_ThenOnlyAScrumBoardGetsTheSections(
        BoardType type,
        ViewSourceKind expected)
    {
        var view = SourceChoices.BoardView(new BoardSummary(6, "Board", type, "GCS"), true);

        Assert.Equal(expected, view.Source);
        Assert.Equal(ViewLayout.List, view.Layout);
    }

    [Fact]
    public void GivenAScrumBoard_WhenOpeningItsBoard_ThenItStaysTheColumnsBoard()
    {
        var view = SourceChoices.BoardView(new BoardSummary(6, "Board", BoardType.Scrum, "GCS"), false);

        Assert.Equal(ViewSourceKind.Board, view.Source);
    }

    [Fact]
    public void GivenFiveThousandIssuesInTwentySprints_WhenLoadedAndMoved_ThenSectionsStayCorrectAndFast()
    {
        var sprints = Enumerable.Range(1, 20)
            .Select(id => new Sprint(id, $"Sprint {id}", SprintState.Future, null, null, null, null, 6))
            .ToArray();
        var rows = Enumerable.Range(0, 5000)
            .Select(number => BacklogFixtures.Issue($"A-{number}", number % 21 == 20 ? null : sprints[number % 21], 1))
            .ToArray();
        var state = Loaded(rows);

        var clock = System.Diagnostics.Stopwatch.StartNew();

        for (var press = 0; press < 50; press++)
        {
            state = Press(state, "j");
        }

        clock.Stop();
        var layout = BacklogSections.Build(Tab(state));

        Assert.Equal(21, layout.Sections.Count);
        Assert.Equal(5000, layout.Sections.Sum(section => section.RowCount));
        Assert.Equal(50, Tab(state).Selected);
        Assert.True(clock.ElapsedMilliseconds < 1000, $"50 motions took {clock.ElapsedMilliseconds} ms");
    }

    private static int ListLayoutRow(int line)
    {
        return ListLayout.HeaderRows + line;
    }

    private static TabState Tab(AppState state)
    {
        return state.Session.Tabs.Single(candidate => candidate.Id == state.Session.Active);
    }

    private static AppState Start()
    {
        var profiles = ProfileCatalog.Empty.With(_work);

        return AppState.Initial with
        {
            Profiles = profiles,
            Session = SiteSession.Empty(profiles.Active) with { Connection = new Connected("Me Myself", "acc-me") },
        };
    }

    private static AppState WithSprints(AppState state)
    {
        var tab = Tab(state) with
        {
            Sprints = [BacklogFixtures.Active, BacklogFixtures.Future, BacklogFixtures.EmptyFuture],
        };
        var tabs = state.Session.Tabs.Select(candidate => candidate.Id == tab.Id ? tab : candidate).ToArray();

        return state with { Session = state.Session with { Tabs = tabs } };
    }

    private AppState Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key, _now)).State;
    }

    private AppState Opened(ViewSourceKind source = ViewSourceKind.ScrumBacklog)
    {
        var definition = new ViewDefinition(source, "6", ViewLayout.List, "Scrum board");
        var opened = _reducer.Reduce(Start(), new ViewOpenRequested(definition)).State;

        return _reducer.Reduce(opened, new ViewportChanged(20)).State;
    }

    private AppState Loaded(
        IReadOnlyList<IssueSummary> rows,
        ViewSourceKind source = ViewSourceKind.ScrumBacklog,
        PageCursor? next = null)
    {
        var opened = Opened(source);
        var tab = Tab(opened);

        return _reducer.Reduce(opened, new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage(rows, next, null), _now))
            .State;
    }
}
