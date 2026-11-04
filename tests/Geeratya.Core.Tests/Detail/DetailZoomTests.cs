using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Detail;

/// <summary>Zoomed sections of the issue detail (#705): zoom, the strip, Esc and the Attachments list.</summary>
[Trait("Os", "Independent")]
public class DetailZoomTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly IssueStatus _todo = new("1", "To Do", "new");
    private static readonly IssueTypeSummary _story = new("10", "Story", 0, false);
    private static readonly IssueAssignee _eve = new("acc-eve", "Eve Example");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Theory]
    [InlineData("description", "Description")]
    [InlineData("subtasks", "Sub-tasks 1")]
    [InlineData("attachments", "Attachments 2")]
    [InlineData("comments", "Comments 2")]
    [InlineData("details", "Details")]
    public void GivenEachSection_WhenZoomed_ThenOnlyThatPaneShowsWithTheZoomedChipAndTheStrip(string pane, string title)
    {
        var state = Zoom(Opened(), pane);

        var document = Document(state);

        Assert.Equal(pane, Top(state).Zoomed);
        Assert.Equal([pane], document.SectionIds);
        Assert.Contains(document.Lines, line => line.PlainText.Contains($"{title} · zoomed"));
        Assert.True(document.IsZoomed);
        Assert.Equal(
            ["description", "subtasks", "remote-links", "attachments", "comments", "details"],
            document.Strip.Select(section => section.Id));
    }

    [Fact]
    public void GivenAZoomedPane_WhenPressingCtrlLAndCtrlH_ThenTheSectionSwitchesAlongTheStripAndStopsAtItsEnds()
    {
        var state = Zoom(Opened(), "description");

        Assert.Equal("description", Top(Press(state, "Ctrl-h").State).Zoomed);

        var next = Press(state, "Ctrl-l").State;

        Assert.Equal("subtasks", Top(next).Zoomed);
        Assert.Equal(["subtasks"], Document(next).SectionIds);
        Assert.Equal("description", Top(Press(next, "Ctrl-h").State).Zoomed);

        var last = Press(state, "Ctrl-l", "Ctrl-l", "Ctrl-l", "Ctrl-l", "Ctrl-l", "Ctrl-l").State;

        Assert.Equal("details", Top(last).Zoomed);
    }

    [Fact]
    public void GivenAZoomedPane_WhenPressingZoomAgain_ThenAllPanesShowWithTheCursorBackInThatPane()
    {
        var state = Press(Zoom(Opened(), "comments"), "Space", "z").State;

        Assert.Null(Top(state).Zoomed);
        Assert.Contains("comments", Document(state).SectionIds);
        Assert.Equal("comments", Document(state).SectionIds[Document(state).SectionAt(Top(state).Cursor)]);
    }

    [Fact]
    public void GivenAZoomedPane_WhenPressingEsc_ThenTheZoomEndsBeforeTheIssueStackPops()
    {
        var zoomed = Zoom(Opened(), "description");

        var first = Press(zoomed, "Esc").State;

        Assert.Null(Top(first).Zoomed);
        Assert.Single(Active(first).Detail);

        var second = Press(first, "Esc").State;

        Assert.Empty(Active(second).Detail);
    }

    [Fact]
    public void GivenAZoomedTab_WhenSwitchingTabsAndBack_ThenTheZoomIsKept()
    {
        var zoomed = Zoom(Opened(), "comments");
        var other = Deliver(zoomed, new ViewOpenRequested(Definition("project = B")));
        var back = Press(other, "Alt-1").State;

        Assert.Equal(Active(zoomed).Id, Active(back).Id);
        Assert.Equal("comments", Top(back).Zoomed);
    }

    [Fact]
    public void GivenTheTitlePane_WhenZooming_ThenItSaysThereIsNothingToZoom()
    {
        var state = Press(Opened(), "Space", "z").State;

        Assert.Null(Top(state).Zoomed);
        Assert.Contains("Nothing to zoom", state.Message);
    }

    [Fact]
    public void GivenAZoomedComment_WhenBuilding_ThenTheBodyIsCutAfterSixLinesNotThree()
    {
        var state = Zoom(OpenedWith(Comment("1", 8)), "comments");
        var text = string.Join('\n', Document(state).Lines.Select(line => line.PlainText));

        Assert.Contains("item 6", text);
        Assert.DoesNotContain("item 7", text);
        Assert.Contains("2 more lines", text);
    }

    [Fact]
    public void GivenALongCommentUnzoomed_WhenBuilding_ThenTheNoteNamesTheZoomKey()
    {
        var state = OpenedWith(Comment("1", 8));
        var text = string.Join('\n', Document(state).Lines.Select(line => line.PlainText));

        Assert.Contains("Enter expand   <space>z zoom", text);
        Assert.DoesNotContain("item 4", text);
    }

    [Fact]
    public void GivenZoomedAttachments_WhenListing_ThenNameTypeAndSizeShowAndNoFileIsRendered()
    {
        var state = Zoom(Opened(), "attachments");
        var text = string.Join('\n', Document(state).Lines.Select(line => line.PlainText));

        Assert.Contains("notes.txt · text/plain · 47 B", text);
        Assert.Contains("photo.png · image/png", text);
    }

    [Theory]
    [InlineData("Enter", "")]
    [InlineData("Space", "o")]
    public void GivenZoomedAttachments_WhenOpening_ThenTheBrowserOpensTheIssuePage(string first, string second)
    {
        var zoomed = Zoom(Opened(), "attachments");

        var opened = second.Length == 0 ? Press(zoomed, first) : Press(zoomed, first, second);

        var request = Assert.Single(opened.Effects.OfType<OpenUrlEffect>());

        Assert.Contains("A-1", request.Url.AbsoluteUri);
    }

    [Fact]
    public void GivenTheAttachmentsRowOfTheDetailsPane_WhenPressingEnter_ThenTheAttachmentsListZooms()
    {
        var state = Opened();

        while (Document(state).Targets[Top(state).Cursor] is not { Value: DetailFieldIds.Attachments })
        {
            state = Press(state, "j").State;
        }

        var zoomed = Press(state, "Enter").State;

        Assert.Equal("attachments", Top(zoomed).Zoomed);
    }

    [Fact]
    public void GivenAZoomedPane_WhenMovingTheCursor_ThenTheStripRowStaysOutOfTheDocumentAndTheRowsCountIt()
    {
        var state = Zoom(Opened(), "comments");
        var ordinary = DetailSize.ListRows(state, Active(Opened()));
        var zoomedRows = DetailSize.ListRows(state, Active(state));

        Assert.Equal(DetailZoom.StripRows, ordinary - zoomedRows);
    }

    [Fact]
    public void GivenAZoomedPaneThatDisappears_WhenTheDataReloads_ThenTheNormalLayoutShowsAgain()
    {
        var state = Zoom(Opened(), "subtasks");
        var tab = Active(state);
        var bare = Issue() with { Subtasks = [] };
        var reloaded = Deliver(state, new IssueDetailLoaded(tab.Id, Top(state).IssueRequest, bare, _now));

        Assert.Null(Top(reloaded).Zoomed);
        Assert.True(Document(reloaded).SectionIds.Count > 1);
        Assert.False(Document(reloaded).IsZoomed);
    }

    [Fact]
    public void GivenTheCursorOnTheSecondAttachment_WhenZoomingAndLeaving_ThenItStaysOnThatAttachment()
    {
        var state = Press(OnPane(Opened(), "attachments"), "j").State;

        Assert.Contains("photo.png", LineAtCursor(state));

        var zoomed = Press(state, "Space", "z").State;

        Assert.Contains("photo.png", LineAtCursor(zoomed));
        Assert.Contains("photo.png", LineAtCursor(Press(zoomed, "Space", "z").State));
    }

    [Fact]
    public void GivenTheCursorOnTheSecondComment_WhenZoomingAndLeaving_ThenItStaysOnThatComment()
    {
        var state = Press(OnPane(Opened(), "comments"), "j").State;
        var second = Document(state).CommentRowIds[CommentsRow(state)];

        var zoomed = Press(state, "Space", "z").State;

        var left = Press(zoomed, "Space", "z").State;

        Assert.Equal(second, Document(zoomed).CommentRowIds[CommentsRow(zoomed)]);
        Assert.Equal(second, Document(left).CommentRowIds[CommentsRow(left)]);
    }

    [Fact]
    public void GivenTheCursorOnAField_WhenZoomingDetailsAndLeaving_ThenItStaysOnThatField()
    {
        var state = Press(OnPane(Opened(), "details"), "j").State;
        var target = Document(state).Targets[Top(state).Cursor];

        var zoomed = Press(state, "Space", "z").State;

        var left = Press(zoomed, "Space", "z").State;

        Assert.Equal(target, Document(zoomed).Targets[Top(zoomed).Cursor]);
        Assert.Equal(target, Document(left).Targets[Top(left).Cursor]);
    }

    private AppState OnPane(AppState state, string pane)
    {
        var steps = 0;

        while (Document(state).SectionIds[Document(state).SectionAt(Top(state).Cursor)] != pane && steps++ < 20)
        {
            state = Press(state, "Ctrl-j").State;
        }

        return state;
    }

    private static string LineAtCursor(AppState state)
    {
        return Document(state).Lines[Top(state).Cursor].PlainText;
    }

    private static int CommentsRow(AppState state)
    {
        var section = Document(state).SectionIds.ToList().IndexOf("comments");

        return Top(state).Cursor - Document(state).Sections[section]
            + (Document(state).PaneScrolls[section]?.ClampedOffset ?? 0);
    }

    private AppState Zoom(AppState state, string pane)
    {
        var current = state;
        var steps = 0;

        while (Document(current).SectionIds[Document(current).SectionAt(Top(current).Cursor)] != pane
            && steps++ < 20)
        {
            current = Press(current, "Ctrl-j").State;
        }

        return Press(current, "Space", "z").State;
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

    private static ViewDefinition Definition(string jql)
    {
        return new ViewDefinition(ViewSourceKind.Jql, jql, ViewLayout.List, jql);
    }

    private AppState Deliver(AppState state, Command command)
    {
        return _reducer.Reduce(state, command).State;
    }

    private ReduceResult Press(AppState state, params string[] keys)
    {
        var result = new ReduceResult(state, []);

        foreach (var key in keys)
        {
            result = _reducer.Reduce(result.State, new KeyPressed(key));
        }

        return result;
    }

    private AppState Opened()
    {
        return OpenedWith(Comment("1", 2), Comment("2", 1));
    }

    private AppState OpenedWith(params IssueComment[] comments)
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with { Profiles = profiles, Session = SiteSession.Empty(profiles.Active) };
        var sized = _reducer.Reduce(start, new ViewportChanged(30, 80)).State;
        var opened = _reducer.Reduce(sized, new ViewOpenRequested(Definition("project = A")));
        var tab = Active(opened.State);
        var row = new IssueSummary("1", "A-1", "Issue 1", _todo, _story, null, null, [], null, null, "A");
        var page = new IssuePage([row], null, null);
        var listed = Deliver(opened.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now));
        var shown = Press(listed, "Enter").State;
        var detail = Active(shown).Detail[0];
        var loaded = Deliver(shown, new IssueDetailLoaded(tab.Id, detail.IssueRequest, Issue(), _now));
        var size = DetailPaging.CommentPageSize;
        var commentPage = new OffsetPage<IssueComment>(comments, 0, size, comments.Length, true);

        return Deliver(loaded, new IssueCommentsLoaded(tab.Id, detail.CommentsRequest, commentPage, _now));
    }

    private static IssueDetail Issue()
    {
        return new IssueDetail(
            "1",
            "A-1",
            "Issue 1",
            _todo,
            _story,
            "High",
            _eve,
            _eve,
            [],
            _now.AddDays(-3),
            _now.AddHours(-1),
            "A",
            AdfMarkdownParser.Parse("A short description."),
            null,
            [new IssueStub("A-18", "Migration", _todo, _story)],
            [],
            [
                new IssueAttachment("1", "notes.txt", "text/plain", 47, _now, null, "https://x/content/1"),
                new IssueAttachment("2", "photo.png", "image/png", 3_500_000, null, null, null),
            ]);
    }

    private static IssueComment Comment(string id, int items)
    {
        var markdown = string.Join('\n', Enumerable.Range(1, items).Select(number => $"- item {number}"));

        return new IssueComment(id, _eve, AdfMarkdownParser.Parse(markdown), _now.AddHours(-2), _now.AddHours(-2));
    }
}
