using Geeratya.Core.Backlog;
using Geeratya.Core.Flash;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Tests.Backlog;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Flash;

/// <summary>The flash jump in the columns layout and in the Scrum backlog (#661).</summary>
[Trait("Os", "Independent")]
public class FlashJumpViewsTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private static readonly BoardConfiguration _configuration = new(
        4,
        "Board",
        BoardType.Kanban,
        [new BoardColumn("To Do", ["1"]), new BoardColumn("Doing", ["2"]), new BoardColumn("Done", ["4"])],
        "10001",
        null,
        null,
        null);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenCardsInSeveralColumns_WhenTypingTheirCommonText_ThenEveryVisibleCardGetsADistinctLetter()
    {
        var state = Press(Board(Cards("1:A-1:Fix login", "2:A-2:Fix export", "4:A-3:Fix logout", "4:A-4:Other")), "Space", "j", "f", "i", "x");

        var labels = FlashJumpReducer.LabelsOf(state, state.FlashJump!);

        Assert.Equal(["A-1", "A-2", "A-3"], labels.Select(label => label.Key).Order());
        Assert.Equal(3, labels.Select(label => label.Letter).Distinct().Count());
        Assert.All(labels, label => Assert.True(label.HasLetter));
        Assert.Equal([0, 1, 2], labels.Select(label => label.Column).Order());
    }

    [Fact]
    public void GivenALabelOnACardInAnotherColumn_WhenTypingItsLetter_ThenTheCursorMovesThereAndThePromptCloses()
    {
        var state = Press(Board(Cards("1:A-1:Fix login", "2:A-2:Fix export", "4:A-3:Fix logout")), "Space", "j", "f", "i", "x");
        var target = FlashJumpReducer.LabelsOf(state, state.FlashJump!).Single(label => label.Key == "A-3");

        var jumped = Press(state, target.Letter.ToString());

        Assert.Null(jumped.FlashJump);
        Assert.Equal(InputMode.Normal, jumped.Mode);
        Assert.Equal("A-3", Active(jumped).SelectedKey);
        Assert.Equal(2, Active(jumped).Cursor!.Column);
    }

    [Fact]
    public void GivenCardsInTheSelectedColumn_WhenPressingEnter_ThenTheNearestCardInThatColumnWins()
    {
        var state = Press(Board(Cards("1:A-1:Fix a", "1:A-2:Fix b", "2:A-3:Fix c")), "j", "Space", "j", "f", "i", "x", "Enter");

        Assert.Equal("A-2", Active(state).SelectedKey);
    }

    [Fact]
    public void GivenACardBelowTheVisibleCards_WhenTypingItsKey_ThenItGetsNoLabel()
    {
        var cards = Enumerable.Range(1, 40).Select(number => $"1:A-{number}:Card {number}").ToArray();
        var state = Press(Board(Cards(cards)), "Space", "j", "A", "-", "4", "0");

        Assert.Empty(FlashJumpReducer.LabelsOf(state, state.FlashJump!));
    }

    [Fact]
    public void GivenALetterThatContinuesAKey_WhenListingLabels_ThenItIsNotALabel()
    {
        var state = Press(Board(Cards("1:A-1:One", "1:A-2:Two", "2:A-3:Three")), "Space", "j", "A", "-");

        var labels = FlashJumpReducer.LabelsOf(state, state.FlashJump!);

        Assert.Equal(3, labels.Count);
        Assert.DoesNotContain(labels, label => label.Letter is '1' or '2' or '3');
    }

    [Fact]
    public void GivenATextOnlyInASecondSummaryLine_WhenTyping_ThenTheCardIsLabelledOnlyWhenTheLineShowsIt()
    {
        var longText = $"{new string('x', 30)} tail words";
        var state = Press(Board(Cards($"1:A-1:{longText}")), "Space", "j", "w", "o", "r", "d", "s");
        var hidden = Press(Board(Cards($"1:A-1:{new string('x', 200)} words")), "Space", "j", "w", "o", "r", "d", "s");

        Assert.Single(FlashJumpReducer.LabelsOf(state, state.FlashJump!));
        Assert.Empty(FlashJumpReducer.LabelsOf(hidden, hidden.FlashJump!));
    }

    [Fact]
    public void GivenABacklog_WhenTypingText_ThenRowsOfEverySectionInViewAreLabelledNotHeaders()
    {
        var state = Press(Backlog(), "Space", "j", "I", "s", "s", "u", "e");

        var labels = FlashJumpReducer.LabelsOf(state, state.FlashJump!);

        Assert.Equal(6, labels.Count);
        Assert.All(labels, label => Assert.True(label.HasLetter));
    }

    [Fact]
    public void GivenABacklogLabel_WhenTypingItsLetter_ThenTheCursorMovesToThatRowInAnotherSection()
    {
        var state = Press(Backlog(), "Space", "j", "I", "s", "s", "u", "e");
        var target = FlashJumpReducer.LabelsOf(state, state.FlashJump!).Single(label => Active(state).Rows[label.Row].Key == "A-1");

        var jumped = Press(state, target.Letter.ToString());

        Assert.Null(jumped.FlashJump);
        Assert.Equal("A-1", Active(jumped).SelectedKey);
        Assert.Equal("A-1", Active(jumped).Rows[Active(jumped).Selected].Key);
    }

    [Fact]
    public void GivenACollapsedSection_WhenTyping_ThenItsHiddenRowsGetNoLabel()
    {
        var collapsed = Active(Backlog()) with { CollapsedSections = [BacklogSections.BacklogKey] };
        var state = Press(WithTab(collapsed), "Space", "j", "I", "s", "s", "u", "e");

        var labels = FlashJumpReducer.LabelsOf(state, state.FlashJump!);

        var keys = labels.Select(label => Active(state).Rows[label.Row].Key).Order().ToArray();

        Assert.Equal(["A-2", "A-3", "A-4", "A-6"], keys);
    }

    private static TabState Active(AppState state)
    {
        return state.Session.Tabs.Single(tab => tab.Id == state.Session.Active);
    }

    private static IssueSummary[] Cards(params string[] specs)
    {
        return [.. specs.Select(Card)];
    }

    /// <summary>A spec is <c>statusId:key:summary</c>.</summary>
    private static IssueSummary Card(string spec)
    {
        var parts = spec.Split(':', 3);

        return new IssueSummary(
            parts[1],
            parts[1],
            parts[2],
            new IssueStatus(parts[0], "Status", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
    }

    private AppState Press(AppState state, params string[] keys)
    {
        foreach (var key in keys)
        {
            state = _reducer.Reduce(state, new KeyPressed(key)).State;
        }

        return state;
    }

    private AppState Board(IssueSummary[] rows)
    {
        var start = AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(_work),
            Session = SiteSession.Empty(_work),
        };
        var definition = new ViewDefinition(ViewSourceKind.Board, "4", ViewLayout.Columns, "Board");
        var opened = _reducer.Reduce(start, new ViewOpenRequested(definition)).State;
        var sized = _reducer.Reduce(opened, new ViewportChanged(20, 100)).State;
        var tab = Active(sized);
        var configured = _reducer.Reduce(sized, new BoardConfigurationLoaded(tab.Id, tab.RequestId, _configuration));
        var page = new IssuePage(rows, null, rows.Length);

        return _reducer.Reduce(configured.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
    }

    private AppState Backlog()
    {
        var tab = BacklogFixtures.Tab(
            BacklogSections.Order(BacklogFixtures.RankedRows),
            [BacklogFixtures.Active, BacklogFixtures.Future, BacklogFixtures.EmptyFuture]);

        return WithTab(tab);
    }

    private AppState WithTab(TabState tab)
    {
        var start = AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(_work),
            Session = SiteSession.Empty(_work) with { Tabs = [tab], Active = tab.Id },
            ViewportColumns = 100,
        };

        return _reducer.Reduce(start, new ViewportChanged(20, 100)).State;
    }
}
