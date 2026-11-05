using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Detail;

[Trait("Os", "Independent")]
public class DetailOverlayTests
{
    private const int Header = 2;

    private static readonly DateTimeOffset _now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueStatus _status = new("1", "To Do", "new");
    private static readonly IssueTypeSummary _type = new("10", "Task", 0, false);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Theory]
    [InlineData(64, 24, 0, 0, 64, 24, true)]
    [InlineData(99, 24, 0, 0, 99, 24, true)]
    [InlineData(100, 24, 4, 5, 92, 17, false)]
    [InlineData(120, 30, 4, 5, 112, 23, false)]
    [InlineData(200, 40, 22, 5, 156, 33, false)]
    [InlineData(120, 9, 4, 2, 112, 5, false)]
    [InlineData(120, 12, 4, 2, 112, 8, false)]
    public void GivenATerminal_WhenPlacing_ThenTheOverlayIsCentredCappedAndFillsBelow100(
        int width,
        int bodyHeight,
        int left,
        int top,
        int boxWidth,
        int boxHeight,
        bool fills)
    {
        var box = DetailOverlayGeometry.Place(width, bodyHeight);

        Assert.Equal(new DetailOverlayGeometry(left, top, boxWidth, boxHeight, fills), box);
        Assert.Equal(width, box.Left + box.Width + (fills ? 0 : box.Left));
    }

    [Fact]
    public void GivenAnOverlay_WhenAskingForACell_ThenOnlyTheBoxContainsIt()
    {
        var box = DetailOverlayGeometry.Place(120, 30);

        Assert.True(box.Contains(2, 4));
        Assert.True(box.Contains(117, 28));
        Assert.False(box.Contains(1, 5));
        Assert.False(box.Contains(118, 5));
        Assert.False(box.Contains(5, 3));
        Assert.False(box.Contains(5, 29));
    }

    [Fact]
    public void GivenTwoIssuesOpen_WhenEscArrives_ThenOneIsPoppedAndTheLastEscClosesTheOverlay()
    {
        var state = TwoOpen();

        var popped = _reducer.Reduce(state, new KeyPressed("Esc")).State;
        var closed = _reducer.Reduce(popped, new KeyPressed("Esc")).State;

        Assert.Equal(["A-1"], Active(popped).Detail.Select(detail => detail.Key));
        Assert.Empty(Active(closed).Detail);
    }

    [Fact]
    public void GivenTwoIssuesOpen_WhenTheBackdropIsClicked_ThenTheWholeStackClosesAndTheViewIsAsLeft()
    {
        var state = TwoOpen();
        var selected = Active(state).Selected;

        var closed = _reducer.Reduce(state, new IssueDetailCloseRequested(Active(state).Id)).State;

        Assert.Empty(Active(closed).Detail);
        Assert.Equal(selected, Active(closed).Selected);
    }

    [Fact]
    public void GivenAWideTerminal_WhenOpeningAnIssue_ThenTheDocumentIsLaidOutForTheOverlayWidth()
    {
        var wide = _reducer.Reduce(Start(), new ViewportChanged(30, 200, 33)).State;
        var opened = Open(wide);

        Assert.Equal(DetailOverlayGeometry.MaxWidth - (2 * DetailOverlayGeometry.CardInsetColumns), Active(opened).Detail[^1].Document!.Width);
    }

    [Fact]
    public void GivenAnOpenIssue_WhenTheTerminalGrowsWide_ThenTheDocumentIsLaidOutAgainForTheOverlay()
    {
        var narrow = Open(_reducer.Reduce(Start(), new ViewportChanged(20, 80, 23)).State);

        var wide = _reducer.Reduce(narrow, new ViewportChanged(30, 120, 33)).State;

        Assert.Equal(80, Active(narrow).Detail[^1].Document!.Width);
        Assert.Equal(112, Active(wide).Detail[^1].Document!.Width);
    }

    [Fact]
    public void GivenAnOverlayInTheBody_WhenClickingOutsideAndInside_ThenOutsideClosesAndInsideDoesNot()
    {
        var state = Open(_reducer.Reduce(Start(), new ViewportChanged(30, 120, 33)).State);
        var context = MouseContext.From(state, 120, 36, Header, _now);

        var outside = MouseResolver.Resolve(context, 0, Header + 5, MouseGesture.Click, _now);
        var inside = MouseResolver.Resolve(context, 60, Header + 5, MouseGesture.Click, _now);
        var margin = MouseResolver.Resolve(context, 60, Header, MouseGesture.Click, _now);

        Assert.Equal(Active(state).Id, Assert.IsType<IssueDetailCloseRequested>(Assert.Single(outside)).Tab);
        Assert.IsType<PointerPlaced>(Assert.Single(inside));
        Assert.IsType<IssueDetailCloseRequested>(Assert.Single(margin));
    }

    [Fact]
    public void GivenAnOverlayInTheBody_WhenTheWheelTurnsOverTheBackdrop_ThenTheDetailStillScrolls()
    {
        var state = Open(_reducer.Reduce(Start(), new ViewportChanged(30, 120, 33)).State);
        var context = MouseContext.From(state, 120, 36, Header, _now);

        var wheel = MouseResolver.Resolve(context, 0, Header + 5, MouseGesture.WheelDown, _now);

        Assert.IsType<DetailWheeled>(Assert.Single(wheel));
    }

    [Fact]
    public void GivenANarrowTerminal_WhenClickingAnywhereInTheBody_ThenNothingClosesBecauseTheOverlayFillsIt()
    {
        var state = Open(_reducer.Reduce(Start(), new ViewportChanged(20, 80, 23)).State);
        var context = MouseContext.From(state, 80, 26, Header, _now);

        var click = MouseResolver.Resolve(context, 0, Header, MouseGesture.Click, _now);

        Assert.IsType<PointerPlaced>(Assert.Single(click));
    }

    [Fact]
    public void GivenAClickInTheOverlay_WhenPlacingTheCursor_ThenTheRowIsCountedFromTheOverlayTop()
    {
        var state = Open(_reducer.Reduce(Start(), new ViewportChanged(30, 120, 33)).State);
        var top = DetailOverlayGeometry.Place(120, 33).Top;

        var document = Active(state).Detail[^1].Document!;
        var onEdge = _reducer.Reduce(state, new PointerPlaced(60, top, 120));
        var below = _reducer.Reduce(state, new PointerPlaced(60, top + 4, 120));
        var above = _reducer.Reduce(state, new PointerPlaced(60, top - 1, 120));

        Assert.Equal(document.Stops[0], Active(onEdge.State).Detail[^1].Cursor);
        Assert.Equal(document.Snap(4), Active(below.State).Detail[^1].Cursor);
        Assert.Equal(state, above.State);
    }

    [Theory]
    [InlineData("l")]
    [InlineData("$")]
    public void GivenABoardWithTwoColumns_WhenAColumnKeyArrivesWithoutAnIssue_ThenTheCursorMoves(string key)
    {
        var board = Board(false);

        var after = Active(_reducer.Reduce(board, new KeyPressed(key)).State);

        Assert.NotEqual(Active(board).SelectedKey, after.SelectedKey);
    }

    [Theory]
    [InlineData("l")]
    [InlineData("$")]
    [InlineData("h")]
    [InlineData("0")]
    public void GivenAnOpenIssueOverABoard_WhenAColumnKeyArrives_ThenTheViewUnderneathIsUnchanged(string key)
    {
        var board = Board(true);

        var result = _reducer.Reduce(board, new KeyPressed(key));

        Assert.Equal(board, result.State);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenAnOpenIssueOverABoardInInsertMode_WhenAKeyArrives_ThenTheGateDoesNotSwallowIt()
    {
        var board = Board(true) with { Mode = InputMode.Insert };

        var result = _reducer.Reduce(board, new KeyPressed("l"));

        Assert.Equal(InputMode.Insert, result.State.Mode);
    }

    private AppState Board(bool withIssue)
    {
        var start = _reducer.Reduce(Start(), new ViewportChanged(30, 120, 33)).State;
        var definition = new ViewDefinition(ViewSourceKind.Board, "4", ViewLayout.Columns, "Board");
        var opened = _reducer.Reduce(start, new ViewOpenRequested(definition)).State;
        var tab = Active(opened);
        var configuration = new BoardConfiguration(
            4,
            "Board",
            BoardType.Kanban,
            [new BoardColumn("To Do", ["1"]), new BoardColumn("Done", ["2"])],
            "10001",
            null,
            null,
            null);
        var configured = _reducer.Reduce(opened, new BoardConfigurationLoaded(tab.Id, tab.RequestId, configuration));
        var page = new IssuePage([Row(1), Row(2, new IssueStatus("2", "Done", "done"))], null, 2);
        var loaded = _reducer.Reduce(configured.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;

        return withIssue ? _reducer.Reduce(loaded, new KeyPressed("Enter")).State : loaded;
    }

    private AppState TwoOpen()
    {
        var first = Open(_reducer.Reduce(Start(), new ViewportChanged(20, 120, 23)).State);

        return _reducer.Reduce(first, new IssueDetailOpenRequested(Active(first).Id, "A-2", null)).State;
    }

    private AppState Open(AppState state, ViewLayout layout = ViewLayout.List)
    {
        var opened = _reducer.Reduce(state, new ViewOpenRequested(Definition() with { Layout = layout }));
        var tab = Active(opened.State);
        var page = new IssuePage([Row(1), Row(2)], null, null);
        var loaded = _reducer.Reduce(opened.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now));

        return _reducer.Reduce(loaded.State, new KeyPressed("Enter")).State;
    }

    private static AppState Start()
    {
        var profiles = ProfileCatalog.Empty.With(_work);

        return AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
    }

    private static TabState Active(AppState state)
    {
        return state.Session.Tabs.Single(tab => tab.Id == state.Session.Active);
    }

    private static ViewDefinition Definition()
    {
        return new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");
    }

    private static IssueSummary Row(int number, IssueStatus? status = null)
    {
        return new IssueSummary(
            $"{number}",
            $"A-{number}",
            $"Issue {number}",
            status ?? _status,
            _type,
            null,
            null,
            [],
            null,
            null,
            "A");
    }
}
