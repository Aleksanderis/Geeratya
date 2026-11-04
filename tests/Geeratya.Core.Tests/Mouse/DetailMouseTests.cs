using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Mouse;

/// <summary>
/// The mouse in the detail overlay and its panes (#714): clicks and the wheel go through the real resolver and
/// reducer, on a 120 by 36 screen where the overlay shows every pane without scrolling.
/// </summary>
[Trait("Os", "Independent")]
public class DetailMouseTests
{
    private const int Width = 120;
    private const int Height = 36;
    private const int Header = 2;
    private const int BodyHeight = Height - 1 - Header;
    private const int MainColumn = 10;

    private static readonly DateTimeOffset _now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueTypeSummary _story = new("10", "Story", 0, false);
    private static readonly IssueAssignee _eve = new("acc-eve", "Eve Example");
    private static readonly DetailOverlayGeometry _box = DetailOverlayGeometry.Place(Width, BodyHeight);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenAnOpenedIssue_WhenListingTheDetailRowsOfTheMouseMap_ThenOnlyTheWheelDoubleClickAndRightClickRemain()
    {
        var gestures = DefaultMouseMap.Rows.Where(row => row.Target == MouseTarget.Detail)
            .Select(row => row.Gesture).Order().ToList();

        Assert.Equal(
            new[] { MouseGesture.DoubleClick, MouseGesture.RightClick, MouseGesture.WheelUp, MouseGesture.WheelDown }
                .Order(),
            gestures);
    }

    [Fact]
    public void GivenTheCursorInTheTitle_WhenTheWheelTurnsOverTheDescriptionPane_ThenThatPaneTakesTheFocusFirst()
    {
        var state = Opened();
        var section = Document(state).SectionIds.ToList().IndexOf(PaneIds.Description);
        var row = Document(state).Sections[section] - Top(state).Scroll;

        var focused = ViewReducer.FocusUnderWheel(state, _box.Left + MainColumn, _box.Top + row);

        Assert.Equal(Document(state).Snap(Document(state).Sections[section]), Top(focused).Cursor);
        Assert.Equal(PaneIds.Description, PaneAt(focused));
    }

    [Fact]
    public void GivenTheCursorInTheMainColumn_WhenTheWheelTurnsOverTheSidebar_ThenTheSidebarTakesTheFocus()
    {
        var state = Opened();

        var result = Click(state, 6, SideColumn(), MouseGesture.WheelDown);

        Assert.NotNull(Top(result.State).SideCursor);
    }

    [Fact]
    public void GivenTheSidebarHasTheFocus_WhenTheWheelTurnsOverTheMainColumn_ThenTheMainColumnTakesTheFocusBack()
    {
        var state = _reducer.Reduce(Opened(), new KeyPressed("Ctrl-l")).State;

        var result = Click(state, 6, MainColumn, MouseGesture.WheelUp);

        Assert.Null(Top(result.State).SideCursor);
    }

    [Fact]
    public void GivenThePointerOverThePaneWithTheFocus_WhenTheWheelTurns_ThenItRunsTheMotionLikeTheKey()
    {
        var state = Opened();
        var row = RowOf(state, FirstLine(state, target => target.Kind == DetailTargetKind.Comment));
        var inComments = Click(state, row, MainColumn).State;

        var wheel = Click(inComments, row, MainColumn, MouseGesture.WheelDown).State;

        var keys = _reducer.Reduce(inComments, new ActionRequested(ActionIds.NavigateDown, 3, _now)).State;
        Assert.Equal(Top(keys).Cursor, Top(wheel).Cursor);
        Assert.Equal(PaneIds.Comments, PaneAt(wheel));
    }

    [Fact]
    public void GivenThePointerOnTheBackdrop_WhenTheWheelTurns_ThenTheFocusStaysAndTheMotionRuns()
    {
        var state = Opened();
        var context = MouseContext.From(state, Width, Height, Header, _now);

        var commands = MouseResolver.Resolve(context, 0, Header + 5, MouseGesture.WheelDown, _now);

        var wheeled = Assert.IsType<DetailWheeled>(Assert.Single(commands));
        var result = _reducer.Reduce(state, wheeled).State;
        var keys = _reducer.Reduce(state, new ActionRequested(ActionIds.NavigateDown, 3, _now)).State;
        Assert.Equal(Top(keys).Cursor, Top(result).Cursor);
        Assert.Null(Top(result).SideCursor);
    }

    [Fact]
    public void GivenASubtaskRow_WhenClickingIt_ThenItOpensInTheStack()
    {
        var state = Opened();
        var line = FirstLine(state, target => target is { Kind: DetailTargetKind.Issue, Value: "A-18" });

        var result = Click(state, RowOf(state, line), MainColumn);

        Assert.Equal(["A-1", "A-18"], Active(result.State).Detail.Select(detail => detail.Key));
        Assert.Contains(result.Effects, effect => effect is LoadIssueDetailEffect);
    }

    [Fact]
    public void GivenACommentHeader_WhenClickingIt_ThenTheCommentIsSelectedAndNothingOpens()
    {
        var state = Opened();
        var line = FirstLine(state, target => target.Kind == DetailTargetKind.Comment);

        var result = Click(state, RowOf(state, line), MainColumn);

        Assert.Equal(line, Top(result.State).Cursor);
        Assert.Single(Active(result.State).Detail);
        Assert.Empty(Top(result.State).Expanded);
        Assert.DoesNotContain(result.Effects, effect => effect is LoadIssueDetailEffect);
    }

    [Fact]
    public void GivenACutCommentBody_WhenClickingItsText_ThenThatCommentIsSelectedNotTheNextOne()
    {
        var state = Opened();
        var header = FirstLine(state, target => target.Kind == DetailTargetKind.Comment);
        var body = header + 1;

        var result = Click(state, RowOf(state, body), MainColumn);

        Assert.DoesNotContain(body, Document(state).Stops);
        Assert.Equal(header, Top(result.State).Cursor);
    }

    [Fact]
    public void GivenACutComment_WhenClickingItsMoreLinesRow_ThenTheCommentExpands()
    {
        var state = Opened();
        var line = FirstLine(state, target => target is { Kind: DetailTargetKind.Comment, Expands: true, Note: true });

        var result = Click(state, RowOf(state, line), MainColumn);

        Assert.Contains("comment:1", Top(result.State).Expanded);
    }

    [Fact]
    public void GivenACutDescription_WhenClickingItsMoreLinesRow_ThenTheDescriptionExpands()
    {
        var state = Opened();
        var line = FirstLine(state, target => target is { Expands: true, Note: true, Kind: DetailTargetKind.Field });

        var result = Click(state, RowOf(state, line), MainColumn);

        Assert.Contains(DetailFieldIds.Description, Top(result.State).Expanded);
    }

    [Fact]
    public void GivenAnEditableDetailsField_WhenClickingItsLabelAndThenItsValue_ThenTheLabelFocusesAndTheValueOpensTheDropdown()
    {
        var state = Editable(Opened());
        var (label, value) = SideRows(state, DetailFieldIds.Priority);

        var focused = Click(state, label, SideColumn());
        var opened = Click(state, value, SideColumn());

        Assert.Equal(DetailFieldIds.Priority, SideTarget(focused.State));
        Assert.Null(Active(focused.State).Form);
        Assert.Equal(DetailFieldIds.Priority, Active(opened.State).Form!.Dropdown);
        Assert.Contains(opened.Effects, effect => effect is LoadFormMetadataEffect);
    }

    [Fact]
    public void GivenAFieldThatCannotBeEdited_WhenClickingItsValue_ThenOnlyTheFocusMoves()
    {
        var state = Editable(Opened());
        var (_, value) = SideRows(state, DetailFieldIds.Reporter);

        var result = Click(state, value, SideColumn());

        Assert.Equal(DetailFieldIds.Reporter, SideTarget(result.State));
        Assert.Null(Active(result.State).Form);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenTheDetailsPaneTopEdge_WhenClickingIt_ThenTheSidebarTakesTheFocus()
    {
        var result = Click(Opened(), 0, SideColumn());

        Assert.NotNull(Top(result.State).SideCursor);
        Assert.Null(Active(result.State).Form);
    }

    [Fact]
    public void GivenAPaneBottomEdge_WhenClickingIt_ThenThatPaneAndNotTheNextOneTakesTheFocus()
    {
        var state = Opened();
        var section = Document(state).SectionIds.ToList().IndexOf(PaneIds.Description);
        var edge = Document(state).PaneStarts[section + 1] - 1;

        var result = Click(state, RowOf(state, edge), MainColumn);

        Assert.Equal(PaneIds.Description, PaneAt(result.State));
    }

    [Fact]
    public void GivenAPaneTopEdge_WhenClickingIt_ThenThePaneTakesTheFocusAndDoesNotFold()
    {
        var state = Opened();
        var section = Document(state).SectionIds.ToList().IndexOf(PaneIds.Description);
        var edge = Document(state).PaneStarts[section];

        var result = Click(state, RowOf(state, edge), MainColumn);

        Assert.Equal(PaneIds.Description, PaneAt(result.State));
        Assert.Equal(Document(state).Lines.Count, Document(result.State).Lines.Count);
    }

    [Fact]
    public void GivenADoubleClickOnAPaneEdge_WhenResolving_ThenOnlyTheFocusMovesAndNothingOpens()
    {
        var state = Opened();
        var section = Document(state).SectionIds.ToList().IndexOf(PaneIds.Subtasks);
        var edge = Document(state).PaneStarts[section];

        var result = Click(state, RowOf(state, edge), MainColumn, MouseGesture.DoubleClick);

        Assert.Single(Active(result.State).Detail);
        Assert.Empty(result.Effects);
        Assert.Equal(PaneIds.Subtasks, PaneAt(result.State));
    }

    [Fact]
    public void GivenAZoomedPane_WhenClickingASectionNameOfTheStrip_ThenThatSectionIsZoomed()
    {
        var zoomed = Zoomed(Opened());
        var chips = DetailStrip.Chips(Document(zoomed), Top(zoomed).Zoomed, Document(zoomed).Width);
        var comments = chips.Single(chip => chip.Id == PaneIds.Comments);

        var result = Click(zoomed, 0, comments.Start + 1);

        Assert.Equal(PaneIds.Comments, Top(result.State).Zoomed);
        Assert.Single(Active(result.State).Detail);
    }

    [Fact]
    public void GivenAZoomedPane_WhenClickingTheGapBetweenTwoSectionNames_ThenNothingChanges()
    {
        var zoomed = Zoomed(Opened());
        var chips = DetailStrip.Chips(Document(zoomed), Top(zoomed).Zoomed, Document(zoomed).Width);
        var gap = chips[0].Start + chips[0].Width;

        var result = Click(zoomed, 0, gap);

        Assert.Equal(Top(zoomed).Zoomed, Top(result.State).Zoomed);
        Assert.Equal(Top(zoomed).Cursor, Top(result.State).Cursor);
    }

    [Fact]
    public void GivenAZoomedPane_WhenDoubleClickingASectionName_ThenItSwitchesWithoutOpeningAnything()
    {
        var zoomed = Zoomed(Opened());
        var chips = DetailStrip.Chips(Document(zoomed), Top(zoomed).Zoomed, Document(zoomed).Width);
        var subtasks = chips.Single(chip => chip.Id == PaneIds.Subtasks);

        var result = Click(zoomed, 0, subtasks.Start + 1, MouseGesture.DoubleClick);

        Assert.Equal(PaneIds.Subtasks, Top(result.State).Zoomed);
        Assert.Single(Active(result.State).Detail);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenTheTitleInItsEditState_WhenClickingAnotherRowOfTheDetail_ThenTheClickIsIgnored()
    {
        var edited = Editing();
        var line = FirstLine(edited, target => target is { Kind: DetailTargetKind.Issue, Value: "A-18" });
        var context = MouseContext.From(edited, Width, Height, Header, _now);

        var click = MouseResolver.Resolve(
            context,
            _box.Left + MainColumn,
            Header + _box.Top + RowOf(edited, line),
            MouseGesture.Click,
            _now);

        Assert.Empty(click);
        Assert.Single(Active(edited).Detail);
        Assert.Equal(PaneIds.Title, Active(edited).Form!.Pane);
    }

    [Theory]
    [InlineData(MouseGesture.DoubleClick)]
    [InlineData(MouseGesture.RightClick)]
    [InlineData(MouseGesture.WheelDown)]
    public void GivenTheTitleInItsEditState_WhenOtherGesturesLandOnTheDetail_ThenNothingHappens(MouseGesture gesture)
    {
        var edited = Editing();
        var context = MouseContext.From(edited, Width, Height, Header, _now);

        var commands = MouseResolver.Resolve(context, _box.Left + MainColumn, Header + _box.Top + 5, gesture, _now);

        Assert.Empty(commands);
    }

    [Fact]
    public void GivenTheTitleInItsEditState_WhenClickingTheBackdrop_ThenTheIssueStaysOpenAndTheEditContinues()
    {
        var edited = Editing();
        var context = MouseContext.From(edited, Width, Height, Header, _now);

        var commands = MouseResolver.Resolve(context, 0, Header + 5, MouseGesture.Click, _now);

        Assert.Empty(commands);
    }

    private ReduceResult Click(AppState state, int row, int column, MouseGesture gesture = MouseGesture.Click)
    {
        var context = MouseContext.From(state, Width, Height, Header, _now);
        var commands = MouseResolver.Resolve(context, _box.Left + column, Header + _box.Top + row, gesture, _now);
        var result = new ReduceResult(state, []);
        var effects = new List<Effect>();

        Assert.NotEmpty(commands);

        foreach (var command in commands)
        {
            result = _reducer.Reduce(result.State, command);
            effects.AddRange(result.Effects);
        }

        return new ReduceResult(result.State, effects);
    }

    private static int SideColumn()
    {
        return DetailLayout.MainWidth(_box.Width) + DetailLayout.SidebarGap + 3;
    }

    private static int FirstLine(AppState state, Func<DetailTarget, bool> match)
    {
        var targets = Document(state).Targets;

        for (var line = 0; line < targets.Count; line++)
        {
            if (targets[line] is { } target && match(target))
            {
                return line;
            }
        }

        throw new InvalidOperationException("No such line in the document.");
    }

    private static int RowOf(AppState state, int line)
    {
        var row = line - Top(state).Scroll;

        Assert.InRange(row, 0, _box.Height - 1);

        return row;
    }

    private static SideRowPair SideRows(AppState state, string field)
    {
        var side = Document(state).Side!;
        var line = side.Targets.ToList().FindIndex(target => target is { Kind: DetailTargetKind.Field } && target.Value == field);
        var start = side.BlockAt(line)!.Start;

        return new SideRowPair(start, start + 1);
    }

    private static string? SideTarget(AppState state)
    {
        return Document(state).Side!.Targets[Top(state).SideCursor!.Value]?.Value;
    }

    private static string PaneAt(AppState state)
    {
        return Document(state).SectionIds[Document(state).SectionAt(Top(state).Cursor)];
    }

    private AppState Editable(AppState state)
    {
        var ids = new[] { DetailFieldIds.Priority, DetailFieldIds.Assignee, DetailFieldIds.Labels };

        return _reducer.Reduce(state, new IssueEditableLoaded(Active(state).Id, "A-1", ids)).State;
    }

    private AppState Zoomed(AppState state)
    {
        var onDescription = _reducer.Reduce(state, new KeyPressed("Ctrl-j")).State;
        var first = _reducer.Reduce(onDescription, new KeyPressed("Space")).State;

        return _reducer.Reduce(first, new KeyPressed("z")).State;
    }

    // The title pane in its edit state, the form loaded with the summary.
    private AppState Editing()
    {
        var opened = _reducer.Reduce(Opened(), new KeyPressed("Enter"));
        var tab = Active(opened.State);
        var field = FormField.Create(FormFieldIds.Summary, "Summary", FormFieldKind.Text, true, [])
            .WithInitial(FormValue.OfText("Issue 1"));

        return _reducer.Reduce(opened.State, new FormMetadataLoaded(tab.Id, tab.Form!.RequestId, [field])).State;
    }

    private static TabState Active(AppState state)
    {
        return state.Session.Tabs.Single(tab => tab.Id == state.Session.Active);
    }

    private static DetailState Top(AppState state)
    {
        return Active(state).Detail[^1];
    }

    private static DetailDocument Document(AppState state)
    {
        return Top(state).Document!;
    }

    private AppState Opened()
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
        var sized = _reducer.Reduce(start, new ViewportChanged(30, Width, BodyHeight)).State;
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "project = A");
        var opened = _reducer.Reduce(sized, new ViewOpenRequested(definition));
        var tab = Active(opened.State);
        var row = new IssueSummary("1", "A-1", "Issue 1", _todo, _story, null, null, [], null, null, "A");
        var page = new IssuePage([row], null, null);
        var listed = _reducer.Reduce(opened.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now)).State;
        var shown = _reducer.Reduce(listed, new KeyPressed("Enter")).State;
        var detail = Active(shown).Detail[0];
        var loaded = _reducer.Reduce(shown, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Issue(), _now)).State;
        var comments = new[] { Comment("1", 8), Comment("2", 1) };
        var commentPage = new OffsetPage<IssueComment>(comments, 0, DetailPaging.CommentPageSize, comments.Length, true);

        return _reducer.Reduce(loaded, new IssueCommentsLoaded(tab.Id, detail.CommentsRequest, commentPage, _now)).State;
    }

    private static IssueDetail Issue()
    {
        var text = string.Join("\n\n", Enumerable.Range(1, 8).Select(number => $"Paragraph {number}."));

        return new IssueDetail(
            "1",
            "A-1",
            "Issue 1",
            _todo,
            _story,
            "High",
            _eve,
            _eve,
            ["backend"],
            _now.AddDays(-3),
            _now.AddHours(-1),
            "A",
            AdfMarkdownParser.Parse(text),
            null,
            [new IssueStub("A-18", "Migration", _todo, _story)],
            [],
            []);
    }

    private static IssueComment Comment(string id, int items)
    {
        var markdown = string.Join('\n', Enumerable.Range(1, items).Select(number => $"- item {number}"));

        return new IssueComment(id, _eve, AdfMarkdownParser.Parse(markdown), _now.AddHours(-2), _now.AddHours(-2));
    }

    private sealed record SideRowPair(int Label, int Value);
}
